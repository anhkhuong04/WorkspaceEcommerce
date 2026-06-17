using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

internal sealed class CustomerOrderService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    IValidator<CustomerOrderListRequest> listValidator) : ICustomerOrderService
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
        var orders = dbContext.Orders
            .Where(order => order.CustomerId == customerId.Value)
            .Where(order => !status.HasValue || order.Status == status.Value)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderCode)
            .ToArray();
        var orderIds = orders.Select(order => order.Id).ToArray();
        var itemCountsByOrderId = dbContext.OrderItems
            .Where(item => orderIds.Contains(item.OrderId))
            .GroupBy(item => item.OrderId)
            .ToDictionary(group => group.Key, group => group.Count());
        var items = orders
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .Select(order => ToListItemDto(order, itemCountsByOrderId.GetValueOrDefault(order.Id)))
            .ToArray();
        var page = new PagedResult<CustomerOrderListItemDto>(
            items,
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            orders.Length);

        return Result<PagedResult<CustomerOrderListItemDto>>.Success(page);
    }

    public Task<Result<CustomerOrderDto>> GetOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Task.FromResult(Result<CustomerOrderDto>.Unauthorized("Customer authentication is required."));
        }

        var order = dbContext.Orders.FirstOrDefault(existing =>
            existing.Id == id &&
            existing.CustomerId == customerId.Value);
        if (order is null)
        {
            return Task.FromResult(Result<CustomerOrderDto>.NotFound("Order was not found."));
        }

        return Task.FromResult(Result<CustomerOrderDto>.Success(ToDetailDto(order, customerId.Value)));
    }

    private CustomerOrderDto ToDetailDto(Order order, Guid customerId)
    {
        var items = dbContext.OrderItems
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.SkuSnapshot)
            .ThenBy(item => item.Id)
            .Select(ToItemDto)
            .ToArray();
        var statusHistory = dbContext.OrderStatusHistories
            .Where(history => history.OrderId == order.Id)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .Select(ToStatusHistoryDto)
            .ToArray();

        return new CustomerOrderDto(
            order.Id,
            order.OrderCode,
            customerId,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.ShippingAddress,
            order.Note,
            order.Subtotal,
            order.ShippingFee,
            order.DiscountAmount,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.CreatedAt,
            order.UpdatedAt,
            items,
            statusHistory);
    }

    private static CustomerOrderListItemDto ToListItemDto(Order order, int itemCount)
    {
        return new CustomerOrderListItemDto(
            order.Id,
            order.OrderCode,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.CreatedAt,
            order.UpdatedAt,
            itemCount);
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
