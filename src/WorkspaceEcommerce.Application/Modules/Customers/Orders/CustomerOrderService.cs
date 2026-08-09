using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

internal sealed class CustomerOrderService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    INotificationService notificationService,
    IValidator<CustomerOrderListRequest> listValidator,
    IOrderShipmentService? shipmentService = null) : ICustomerOrderService
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
                dbContext.OrderItems.Count(item => item.OrderId == order.Id)))
            .ToArrayAsyncSafe(cancellationToken);
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

        var order = await dbContext.Orders
            .Where(existing => existing.Id == id && existing.CustomerId == customerId.Value)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<CustomerOrderDto>.NotFound("Order was not found.");
        }

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
        {
            return Result<CustomerOrderDto>.Failure("Order cannot be cancelled at this stage. Only Pending or Confirmed orders can be cancelled.");
        }

        var cancelNote = string.IsNullOrWhiteSpace(reason)
            ? "Cancelled by customer."
            : $"Cancelled by customer: {reason.Trim()}";

        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Cancelled, cancelNote, "Customer");

        // Restore variant stocks
        var orderItems = await dbContext.OrderItems
            .Where(item => item.OrderId == order.Id)
            .ToArrayAsyncSafe(cancellationToken);
        var variantIds = orderItems.Select(item => item.ProductVariantId).Distinct().ToArray();
        var variantsById = (await dbContext.ProductVariants
            .Where(variant => variantIds.Contains(variant.Id))
            .ToArrayAsyncSafe(cancellationToken))
            .ToDictionary(variant => variant.Id);

        foreach (var orderItem in orderItems)
        {
            if (variantsById.TryGetValue(orderItem.ProductVariantId, out var variant))
            {
                variant.RestoreStock(orderItem.Quantity);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(order.TrackingCode) && shipmentService is not null)
        {
            await shipmentService.QueueCancelAsync(order.Id, cancelNote, cancellationToken);
        }

        // Send real-time notification
        await notificationService.NotifyCustomerAsync(
            customerId.Value,
            "order_status_changed",
            new { orderId = order.Id, orderCode = order.OrderCode, newStatus = (int)order.Status },
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

        var order = await dbContext.Orders
            .Where(existing => existing.Id == id && existing.CustomerId == customerId.Value)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<CustomerOrderDto>.NotFound("Order was not found.");
        }

        if (order.Status != OrderStatus.Completed)
        {
            return Result<CustomerOrderDto>.Failure("Only completed orders can be returned.");
        }

        var returnNote = string.IsNullOrWhiteSpace(reason)
            ? "Return requested by customer."
            : $"Return requested: {reason.Trim()}";

        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Returned, returnNote, "Customer");

        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCustomerAsync(
            customerId.Value,
            "order_status_changed",
            new { orderId = order.Id, orderCode = order.OrderCode, newStatus = (int)order.Status },
            cancellationToken);

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
            statusHistory);
    }

    private static OrderItemDto ToItemDto(OrderItem item)
    {
        return new OrderItemDto(
            item.Id,
            item.ProductVariantId,
            item.ProductNameSnapshot,
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
            history.Note,
            history.ChangedAt);
    }
}
