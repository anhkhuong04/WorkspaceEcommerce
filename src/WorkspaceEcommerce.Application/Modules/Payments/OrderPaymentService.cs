using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Modules.Payments;

internal sealed class OrderPaymentService(IAppDbContext db, IShippingCheckoutStore shipping,
    IFulfillmentCoordinator fulfillment, ILoyaltyService loyalty, TimeProvider clock,
    IValidator<ConfirmOrderPaymentRequest> validator) : IOrderPaymentService
{
    public async Task<Result<OrderPaymentSummaryDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return order is null ? Result<OrderPaymentSummaryDto>.NotFound("Order was not found.")
            : Result<OrderPaymentSummaryDto>.Success(await SummaryAsync(order, ct));
    }
    public async Task<Result<OrderPaymentSummaryDto>> ConfirmAsync(Guid id, OrderPaymentReceiptKind kind,
        ConfirmOrderPaymentRequest request, string actor, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<OrderPaymentSummaryDto>.Validation(validation.Errors.Select(x => x.ErrorMessage));
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(actor) || actor.Length > 256)
            return Result<OrderPaymentSummaryDto>.Validation(["Authenticated actor and valid confirmation kind are required."]);
        try
        {
            return await shipping.TransactionAsync(async token =>
            {
                // Match payment callbacks and cancellation: payment rows, then order,
                // then shipment. Never take these locks in the opposite order.
                await db.FindPendingPaymentTransactionsForOrderForUpdateAsync(id, token);
                var order = await db.FindOrderForUpdateAsync(id, token);
                if (order is null) return Result<OrderPaymentSummaryDto>.NotFound("Order was not found.");
                if (await fulfillment.MethodAsync(id, token) is null)
                    return Result<OrderPaymentSummaryDto>.Conflict("Legacy orders retain their existing payment workflow.");
                if (request.Amount != order.TotalAmount || request.Currency != order.CurrencyCode)
                    return Result<OrderPaymentSummaryDto>.Conflict("Confirm the exact frozen order amount and currency. Partial/excess amounts require exception handling.");
                var existing = await shipping.PaymentReceipts.SingleOrDefaultAsync(x => x.OrderId == id && x.Kind == kind, token);
                if (existing is not null)
                    return existing.Reference == request.Reference.Trim()
                        ? Result<OrderPaymentSummaryDto>.Success(await SummaryAsync(order, token))
                        : Result<OrderPaymentSummaryDto>.Conflict("This payment fact was already recorded with another reference.");
                if (order.Status is OrderStatus.Cancelled or OrderStatus.Returned)
                    return Result<OrderPaymentSummaryDto>.Conflict("Cancelled or returned orders cannot accept payment confirmations.");
                var now = clock.GetUtcNow();
                if (kind == OrderPaymentReceiptKind.ManualTransfer)
                {
                    if (order.PaymentMethod != PaymentMethod.ManualBankTransfer || order.PaymentStatus != PaymentStatus.Pending)
                        return Result<OrderPaymentSummaryDto>.Conflict("This order cannot accept a manual transfer confirmation.");
                    order.MarkPaymentPaid(now);
                    await fulfillment.AcceptAsync(order, null, token);
                }
                else
                {
                    if (order.PaymentMethod != PaymentMethod.Cod) return Result<OrderPaymentSummaryDto>.Conflict("This is not a COD order.");
                    if (kind == OrderPaymentReceiptKind.CodCollected)
                    {
                        if (order.Status != OrderStatus.Shipping || order.PaymentStatus != PaymentStatus.Unpaid)
                            return Result<OrderPaymentSummaryDto>.Conflict("COD collection requires an eligible order out for delivery.");
                        order.MarkPaymentPaid(now);
                        var shipment = await db.FindOrderShipmentForUpdateAsync(order.Id, token);
                        if (shipment?.ProviderStatus == "Delivered")
                        {
                            db.Add(order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, "Full COD collection confirmed after recorded delivery.", actor));
                            await shipping.SavePaymentReceiptAsync(token);
                            var earned = await loyalty.EarnForCompletedOrderAsync(order.Id, token);
                            if (earned.IsFailure) throw new InvalidOperationException("COD completion loyalty could not be persisted.");
                        }
                    }
                    else if (!await shipping.PaymentReceipts.AnyAsync(x => x.OrderId == id && x.Kind == OrderPaymentReceiptKind.CodCollected, token))
                        return Result<OrderPaymentSummaryDto>.Conflict("COD must be collected before it can be remitted.");
                }
                db.Add(new OrderPaymentReceipt(Guid.NewGuid(), id, kind, request.Amount, request.Currency,
                    request.Reference, request.Reason, actor, now));
                await shipping.SavePaymentReceiptAsync(token);
                return Result<OrderPaymentSummaryDto>.Success(await SummaryAsync(order, token));
            }, ct);
        }
        catch (DomainException exception) { return Result<OrderPaymentSummaryDto>.Conflict(exception.Message); }
        catch (PersistenceConcurrencyException exception) { return Result<OrderPaymentSummaryDto>.Conflict(exception.Message); }
    }
    private async Task<OrderPaymentSummaryDto> SummaryAsync(Order order, CancellationToken ct)
    {
        var method = await fulfillment.MethodAsync(order.Id, ct);
        var receipts = await shipping.PaymentReceipts.AsNoTracking().Where(x => x.OrderId == order.Id).OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id)
            .Select(x => new OrderPaymentReceiptDto(x.Id, x.Kind, x.Amount, x.Currency, x.Reference, x.Reason, x.Actor, x.OccurredAtUtc)).ToArrayAsync(ct);
        var open = method.HasValue && order.Status is not (OrderStatus.Cancelled or OrderStatus.Returned);
        return new(order.Id, order.PaymentMethod, order.PaymentStatus, order.TotalAmount, order.CurrencyCode, method,
            open && order.PaymentMethod == PaymentMethod.ManualBankTransfer && order.PaymentStatus == PaymentStatus.Pending,
            open && order.PaymentMethod == PaymentMethod.Cod && order.Status == OrderStatus.Shipping && order.PaymentStatus == PaymentStatus.Unpaid,
            open && receipts.Any(x => x.Kind == OrderPaymentReceiptKind.CodCollected) && receipts.All(x => x.Kind != OrderPaymentReceiptKind.CodRemitted), receipts);
    }
}
