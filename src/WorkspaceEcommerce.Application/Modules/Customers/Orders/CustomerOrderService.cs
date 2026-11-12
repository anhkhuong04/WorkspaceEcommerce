using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

internal sealed class CustomerOrderService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    INotificationService notificationService,
    IValidator<CustomerOrderListRequest> listValidator,
    IOrderShipmentService? shipmentService = null,
    IFulfillmentCoordinator? fulfillment = null, IOrderShippingReader? shippingReader = null, TimeProvider? clock = null) : ICustomerOrderService
{
    public async Task<Result<PagedResult<CustomerOrderListItemDto>>> GetOrdersAsync(
        CustomerOrderListRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await listValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PagedResult<CustomerOrderListItemDto>>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<PagedResult<CustomerOrderListItemDto>>.Unauthorized("Customer authentication is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var status = request.Status;
        var query = dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(order => order.CustomerId == customerId.Value)
            .Where(order => !status.HasValue || order.Status == status.Value);
        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var items = await query
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderCode)
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .Select(order => new CustomerOrderListItemDto(
                order.Id,
                order.OrderCode,
                order.TotalAmount,
                order.Status,
                order.PaymentMethod,
                order.PaymentStatus,
                order.PaidAt,
                order.CreatedAt,
                order.UpdatedAt,
                dbContext.OrderItems.Count(item => item.OrderId == order.Id),
                order.TrackingCode,
                dbContext.OrderShipments
                    .Where(shipment => shipment.OrderId == order.Id)
                    .Select(shipment => shipment.Provider)
                    .FirstOrDefault()))
            .ToArrayAsyncSafe(cancellationToken);
        if (shippingReader is not null)
        {
            var summaries = await shippingReader.GetManyAsync(items.Select(x => x.Id).ToArray(), cancellationToken);
            items = items.Select(item => summaries.TryGetValue(item.Id, out var summary)
                ? item with { Shipping = summary, TrackingCode = summary.TrackingCode ?? item.TrackingCode, ShipmentProvider = summary.Method.ToString() } : item).ToArray();
        }
        var page = new PagedResult<CustomerOrderListItemDto>(
            items,
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            totalCount);

        return Result<PagedResult<CustomerOrderListItemDto>>.Success(page);
    }

    public async Task<Result<CustomerOrderDto>> GetOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerOrderDto>.Unauthorized("Customer authentication is required.");
        }

        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(existing => existing.Id == id && existing.CustomerId == customerId.Value)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<CustomerOrderDto>.NotFound("Order was not found.");
        }

        return Result<CustomerOrderDto>.Success(await ToDetailDtoAsync(order, customerId.Value, cancellationToken));
    }

    public async Task<Result<CustomerOrderDto>> CancelOrderAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerOrderDto>.Unauthorized("Customer authentication is required.");
        }

        var cancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (cancellationReason is null)
        {
            return Result<CustomerOrderDto>.Validation(["Cancellation reason is required."]);
        }

        if (cancellationReason.Length > 500)
        {
            return Result<CustomerOrderDto>.Validation(["Cancellation reason must not exceed 500 characters."]);
        }

        Order? order = null;
        var invalidStage = false;
        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var pendingPaymentTransactions = await dbContext.FindPendingPaymentTransactionsForOrderForUpdateAsync(
                    id,
                    transactionCancellationToken);
                order = await dbContext.FindOrderForUpdateAsync(id, transactionCancellationToken);
                if (order is null || order.CustomerId != customerId.Value)
                {
                    order = null;
                    return;
                }

                if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
                {
                    invalidStage = true;
                    return;
                }

                var previousStatus = order.Status;
                var history = order.ChangeStatus(
                    Guid.NewGuid(),
                    OrderStatus.Cancelled,
                    internalNote: null,
                    cancellationReason: cancellationReason,
                    customerMessage: null,
                    changedBy: "Customer");
                dbContext.Add(history);

                await OrderCancellationEffects.ApplyAsync(dbContext, order, previousStatus, pendingPaymentTransactions,
                    cancellationReason, DateTimeOffset.UtcNow, transactionCancellationToken);
                if (fulfillment is not null && await fulfillment.MethodAsync(order.Id, transactionCancellationToken) is not null)
                    await fulfillment.CancelAsync(order, cancellationReason, transactionCancellationToken, $"customer:{customerId.Value}");
                await dbContext.SaveChangesAsync(transactionCancellationToken);
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<CustomerOrderDto>.Conflict(exception.Message);
        }

        if (order is null)
        {
            return Result<CustomerOrderDto>.NotFound("Order was not found.");
        }

        if (invalidStage)
        {
            return Result<CustomerOrderDto>.Failure("Order cannot be cancelled at this stage. Only Pending or Confirmed orders can be cancelled.");
        }

        if (!string.IsNullOrWhiteSpace(order.TrackingCode) && shipmentService is not null
            && (fulfillment is null || await fulfillment.MethodAsync(order.Id, cancellationToken) is null))
        {
            await shipmentService.QueueCancelAsync(order.Id, cancellationReason, cancellationToken);
        }

        await notificationService.NotifyCustomerAsync(
            customerId.Value,
            "order_status_changed",
            new
            {
                orderId = order.Id,
                orderCode = order.OrderCode,
                newStatus = (int)order.Status,
                paymentStatus = (int)order.PaymentStatus,
                cancellationReason
            },
            cancellationToken);

        return Result<CustomerOrderDto>.Success(await ToDetailDtoAsync(order, customerId.Value, cancellationToken));
    }

    public async Task<Result<CustomerOrderDto>> RequestReturnAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerOrderDto>.Unauthorized("Customer authentication is required.");
        }

        Order? order = null;
        try
        {
            await dbContext.ExecuteInTransactionAsync(async ct =>
            {
                order = await dbContext.FindOrderForUpdateAsync(id, ct);
                if (order is null || order.CustomerId != customerId) { order = null; return; }
                if (order.RequestReturn(reason, (clock ?? TimeProvider.System).GetUtcNow()))
                {
                    dbContext.Add(new OrderStatusHistory(Guid.NewGuid(), order.Id, order.Status, order.Status,
                        internalNote: "Return request awaiting staff review; goods and money have not been returned.",
                        cancellationReason: null, customerMessage: "Return requested: " + order.ReturnReason, changedBy: "Customer"));
                    await dbContext.SaveChangesAsync(ct);
                }
            }, cancellationToken);
        }
        catch (DomainException exception) { return Result<CustomerOrderDto>.Conflict(exception.Message); }
        if (order is null) return Result<CustomerOrderDto>.NotFound("Order was not found.");

        return Result<CustomerOrderDto>.Success(await ToDetailDtoAsync(order, customerId.Value, cancellationToken));
    }

    private async Task<CustomerOrderDto> ToDetailDtoAsync(
        Order order,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.OrderItems
            .AsNoTrackingIfEf()
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.SkuSnapshot)
            .ThenBy(item => item.Id)
            .Select(item => ToItemDto(item))
            .ToArrayAsyncSafe(cancellationToken);
        var statusHistory = await dbContext.OrderStatusHistories
            .AsNoTrackingIfEf()
            .Where(history => history.OrderId == order.Id)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .Select(history => ToStatusHistoryDto(history))
            .ToArrayAsyncSafe(cancellationToken);

        return new CustomerOrderDto(
            order.Id,
            order.OrderCode,
            customerId,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.ShippingAddress,
            order.Note,
            order.CouponId,
            order.CouponCodeSnapshot,
            order.CouponNameSnapshot,
            order.Subtotal,
            order.ShippingFee,
            order.DiscountAmount,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.PaymentStatus,
            order.PaidAt,
            order.CreatedAt,
            order.UpdatedAt,
            order.TrackingCode,
            order.ShipmentId,
            items,
            statusHistory) { PaymentDueAt = order.PaymentDueAt, CanRequestReturn = order.Status == OrderStatus.Completed && order.ReturnRequestedAt is null && order.CompletedAt.HasValue && order.CompletedAt.Value.AddDays(7) >= (clock ?? TimeProvider.System).GetUtcNow(), ReturnRequestedAt = order.ReturnRequestedAt, ReturnReviewedAt = order.ReturnReviewedAt, ReturnDecision = order.ReturnDecision, ReturnDecisionReason = order.ReturnDecisionReason, RefundedAmount = order.RefundedAmount, ReturnReason = order.ReturnReason, ReturnRequestDeadline = order.CompletedAt?.AddDays(7), Shipping = shippingReader is null ? null : await shippingReader.GetAsync(order.Id, cancellationToken) };
    }

    private static OrderItemDto ToItemDto(OrderItem item)
    {
        return new OrderItemDto(
            item.Id,
            item.ProductVariantId,
            item.ProductNameSnapshot,
            item.ProductImageUrlSnapshot,
            item.SkuSnapshot,
            item.UnitPrice,
            item.Quantity,
            item.LineTotal,
            item.RequiresInstallation);
    }

    private static CustomerOrderStatusHistoryDto ToStatusHistoryDto(OrderStatusHistory history)
    {
        return new CustomerOrderStatusHistoryDto(
            history.Id,
            history.FromStatus,
            history.ToStatus,
            history.CancellationReason,
            history.CustomerMessage,
            history.ChangedAt);
    }
}
