using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed record WorkspaceShipmentRequest(Guid RequestId, long ExpectedRevision, WorkspaceShipmentAction Action, string Reason, string? AssignedTo = null);
public sealed class WorkspaceShipmentValidator : AbstractValidator<WorkspaceShipmentRequest>
{
    public WorkspaceShipmentValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
        RuleFor(x => x.Action).IsInEnum().NotEqual(WorkspaceShipmentAction.Cancel).WithMessage("Cancel through the order workflow.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AssignedTo).MaximumLength(120);
        RuleFor(x => x.AssignedTo).NotEmpty().When(x => x.Action == WorkspaceShipmentAction.Assign);
    }
}
public sealed record WorkspaceShipmentDto(Guid Id, Guid OrderId, string TrackingCode, string Status, long Revision,
    string? AssignedTo, OrderStatus OrderStatus, PaymentStatus PaymentStatus, bool CanAssign, bool CanDispatch,
    bool CanDeliver, bool CanFailDelivery, bool CanRetryDelivery, bool CanCancel);
public sealed record WorkspaceShipmentAuditDto(Guid Id, WorkspaceShipmentAction Action, long Revision, string Status,
    string Actor, string Reason, string? AssignedTo, DateTimeOffset OccurredAtUtc);
public interface IWorkspaceShipmentService
{
    Task<Result<WorkspaceShipmentDto>> GetAsync(Guid orderId, CancellationToken ct);
    Task<Result<WorkspaceShipmentDto>> ActAsync(Guid orderId, WorkspaceShipmentRequest request, string actor, CancellationToken ct);
    Task<Result<PagedResult<WorkspaceShipmentAuditDto>>> HistoryAsync(Guid orderId, int page, CancellationToken ct);
}
internal sealed class WorkspaceShipmentService(IAppDbContext db, IShippingCheckoutStore shipping, IFulfillmentCoordinator fulfillment,
    IValidator<WorkspaceShipmentRequest> validator, ILoyaltyService loyalty, INotificationService notifications,
    TimeProvider clock, ILogger<WorkspaceShipmentService> logger) : IWorkspaceShipmentService
{
    public async Task<Result<WorkspaceShipmentDto>> GetAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        var shipment = await db.OrderShipments.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId && x.Provider == nameof(ShippingMethod.WorkspaceEC), ct);
        return order is null || shipment is null ? Result<WorkspaceShipmentDto>.NotFound("WorkspaceEC shipment was not found.") : Result<WorkspaceShipmentDto>.Success(ToDto(order, shipment));
    }
    public async Task<Result<WorkspaceShipmentDto>> ActAsync(Guid orderId, WorkspaceShipmentRequest request, string actor, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<WorkspaceShipmentDto>.Validation(validation.Errors.Select(x => x.ErrorMessage));
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 256) return Result<WorkspaceShipmentDto>.Unauthorized("Admin session is required.");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { orderId, request.ExpectedRevision, request.Action, reason = request.Reason.Trim(), assignedTo = request.AssignedTo?.Trim() })));
        bool changed = false;
        Guid? customerId = null;
        try
        {
            var result = await shipping.TransactionAsync(async token =>
            {
                await db.FindPendingPaymentTransactionsForOrderForUpdateAsync(orderId, token);
                var order = await db.FindOrderForUpdateAsync(orderId, token);
                if (order is null || await fulfillment.MethodAsync(orderId, token) != ShippingMethod.WorkspaceEC)
                    return Result<WorkspaceShipmentDto>.NotFound("WorkspaceEC shipment was not found.");
                var shipment = await db.FindOrderShipmentForUpdateAsync(orderId, token);
                if (shipment is null || shipment.Provider != nameof(ShippingMethod.WorkspaceEC)) return Result<WorkspaceShipmentDto>.NotFound("WorkspaceEC shipment was not found.");
                var duplicate = await shipping.WorkspaceOperations.SingleOrDefaultAsync(x => x.Id == request.RequestId, token);
                if (duplicate is not null) return duplicate.ShipmentId == shipment.Id && duplicate.PayloadHash == hash
                    ? Result<WorkspaceShipmentDto>.Success(ToDto(order, shipment)) : Result<WorkspaceShipmentDto>.Conflict("This operation ID was used for another action.");
                if (shipment.Revision != request.ExpectedRevision) return Result<WorkspaceShipmentDto>.Conflict("Shipment revision changed. Reload before retrying.");
                if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed or OrderStatus.Returned) return Result<WorkspaceShipmentDto>.Conflict("Order is no longer eligible for delivery operations.");
                var before = order.Status;
                switch (request.Action)
                {
                    case WorkspaceShipmentAction.Assign:
                        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Processing)) throw new DomainException("Assignment requires an order awaiting dispatch.");
                        break;
                    case WorkspaceShipmentAction.Dispatch:
                        if (order.Status is not (OrderStatus.Confirmed or OrderStatus.Processing) || !PaymentEligible(order)) throw new DomainException("Dispatch requires confirmation and eligible payment.");
                        if (order.Status == OrderStatus.Confirmed) Transition(order, OrderStatus.Processing, actor);
                        Transition(order, OrderStatus.Shipping, actor);
                        break;
                    case WorkspaceShipmentAction.RetryDelivery:
                        if (order.Status != OrderStatus.FailedDelivery || !PaymentEligible(order)) throw new DomainException("Retry requires failed delivery and eligible payment.");
                        Transition(order, OrderStatus.Shipping, actor);
                        break;
                    case WorkspaceShipmentAction.FailDelivery:
                        if (order.Status != OrderStatus.Shipping) throw new DomainException("Only an order out for delivery can fail delivery.");
                        Transition(order, OrderStatus.FailedDelivery, actor);
                        break;
                    case WorkspaceShipmentAction.Deliver:
                        if (order.Status != OrderStatus.Shipping || !PaymentEligible(order)) throw new DomainException("Delivery requires an order out for delivery and eligible payment.");
                        if (order.PaymentStatus == PaymentStatus.Paid) Transition(order, OrderStatus.Completed, actor);
                        break;
                }
                var now = clock.GetUtcNow();
                shipment.ApplyWorkspaceAction(request.Action, request.AssignedTo, now);
                db.Add(new WorkspaceShipmentOperation(request.RequestId, shipment.Id, request.Action, hash, shipment.Revision,
                    shipment.ProviderStatus, actor, request.Reason, shipment.AssignedTo, now));
                db.Add(new ShipmentTimelineEntry(Guid.NewGuid(), shipment.Id, shipment.ProviderStatus, "WorkspaceEC delivery status updated.", now, ShipmentTimelineSource.LocalOperation, null, now));
                await shipping.SaveWorkspaceOperationAsync(token);
                if (order.Status == OrderStatus.Completed)
                {
                    var earned = await loyalty.EarnForCompletedOrderAsync(orderId, token);
                    if (earned.IsFailure) throw new InvalidOperationException("Delivery completion loyalty could not be persisted.");
                }
                changed = before != order.Status; customerId = order.CustomerId;
                return Result<WorkspaceShipmentDto>.Success(ToDto(order, shipment));
            }, ct);
            if (changed && customerId.HasValue && result.IsSuccess)
            {
                try { await notifications.NotifyCustomerAsync(customerId.Value, "order_status_changed", new { orderId, newStatus = (int)result.Value!.OrderStatus }, ct); }
                catch (Exception exception) when (!ct.IsCancellationRequested) { logger.LogWarning(exception, "Delivery notification failed for order {OrderId}", orderId); }
            }
            return result;
        }
        catch (DomainException exception) { return Result<WorkspaceShipmentDto>.Conflict(exception.Message); }
        catch (PersistenceConcurrencyException) { return Result<WorkspaceShipmentDto>.Conflict("Shipment changed concurrently. Reload before retrying."); }
    }
    public async Task<Result<PagedResult<WorkspaceShipmentAuditDto>>> HistoryAsync(Guid orderId, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100000) return Result<PagedResult<WorkspaceShipmentAuditDto>>.Validation(["Invalid history page."]);
        var shipmentId = await db.OrderShipments.Where(x => x.OrderId == orderId && x.Provider == nameof(ShippingMethod.WorkspaceEC)).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (!shipmentId.HasValue) return Result<PagedResult<WorkspaceShipmentAuditDto>>.NotFound("WorkspaceEC shipment was not found.");
        var query = shipping.WorkspaceOperations.AsNoTracking().Where(x => x.ShipmentId == shipmentId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Revision).Skip((page - 1) * 20).Take(20)
            .Select(x => new WorkspaceShipmentAuditDto(x.Id, x.Action, x.Revision, x.Status, x.Actor, x.Reason, x.AssignedTo, x.OccurredAtUtc)).ToArrayAsync(ct);
        return Result<PagedResult<WorkspaceShipmentAuditDto>>.Success(new(items, page, 20, count));
    }
    private void Transition(Order order, OrderStatus status, string actor) => db.Add(order.ChangeStatus(Guid.NewGuid(), status, "WorkspaceEC delivery status updated.", actor));
    private static bool PaymentEligible(Order order) => order.PaymentMethod == PaymentMethod.Cod && order.PaymentStatus is PaymentStatus.Unpaid or PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Paid;
    private static WorkspaceShipmentDto ToDto(Order order, OrderShipment shipment)
    {
        var open = order.Status is not (OrderStatus.Cancelled or OrderStatus.Completed or OrderStatus.Returned);
        var outForDelivery = open && order.Status == OrderStatus.Shipping && shipment.ProviderStatus == "OutForDelivery";
        return new(shipment.Id, order.Id, shipment.TrackingCode, shipment.ProviderStatus, shipment.Revision, shipment.AssignedTo, order.Status, order.PaymentStatus,
            open && shipment.ProviderStatus is "AwaitingRelease" or "Assigned",
            open && shipment.ProviderStatus == "Assigned" && order.Status is OrderStatus.Confirmed or OrderStatus.Processing && PaymentEligible(order),
            outForDelivery && PaymentEligible(order), outForDelivery, open && shipment.ProviderStatus == "FailedDelivery" && PaymentEligible(order),
            open && order.Status is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.FailedDelivery);
    }
}
