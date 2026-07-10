using FluentValidation;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class AdminOrderService(
    IAppDbContext dbContext,
    IValidator<AdminOrderListRequest> listValidator,
    IValidator<UpdateOrderStatusRequest> updateStatusValidator,
    ILoyaltyService loyaltyService,
    ILogger<AdminOrderService> logger) : IAdminOrderService
{
    public async Task<Result<PagedResult<AdminOrderListItemDto>>> GetOrdersAsync(
        AdminOrderListRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await listValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PagedResult<AdminOrderListItemDto>>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSearch = NormalizeOptional(request.Search);
        var status = request.Status;
        var orders = dbContext.Orders
            .Where(order => !status.HasValue || order.Status == status.Value)
            .ToArray()
            .Where(order => normalizedSearch is null || MatchesSearch(order, normalizedSearch))
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.OrderCode)
            .ToArray();

        var itemCountsByOrderId = dbContext.OrderItems
            .GroupBy(item => item.OrderId)
            .ToDictionary(group => group.Key, group => group.Count());
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var items = orders
            .Skip(request.Skip)
            .Take(pageSize)
            .Select(order => ToListItemDto(order, itemCountsByOrderId.GetValueOrDefault(order.Id)))
            .ToArray();
        var page = new PagedResult<AdminOrderListItemDto>(
            items,
            pageNumber,
            pageSize,
            orders.Length);

        return Result<PagedResult<AdminOrderListItemDto>>.Success(page);
    }

    public Task<Result<AdminOrderDto>> GetOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var order = dbContext.Orders.FirstOrDefault(existing => existing.Id == id);
        if (order is null)
        {
            return Task.FromResult(Result<AdminOrderDto>.NotFound("Order was not found."));
        }

        return Task.FromResult(Result<AdminOrderDto>.Success(ToDetailDto(order)));
    }

    public async Task<Result<AdminOrderDto>> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        string? changedBy,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateStatusValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminOrderDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var order = dbContext.Orders.FirstOrDefault(existing => existing.Id == id);
        if (order is null)
        {
            return Result<AdminOrderDto>.NotFound("Order was not found.");
        }

        try
        {
            var history = order.ChangeStatus(
                Guid.NewGuid(),
                request.Status,
                request.Note,
                NormalizeOptional(changedBy));

            dbContext.Add(history);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (request.Status == OrderStatus.Completed)
            {
                await TryEarnLoyaltyPointsAsync(order.Id, cancellationToken);
            }

            return Result<AdminOrderDto>.Success(ToDetailDto(order));
        }
        catch (DomainException exception)
        {
            return Result<AdminOrderDto>.Conflict(exception.Message);
        }
    }

    private async Task TryEarnLoyaltyPointsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await loyaltyService.EarnForCompletedOrderAsync(orderId, cancellationToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Could not earn loyalty points for completed order {OrderId}: {Errors}",
                    orderId,
                    string.Join("; ", result.Errors));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to earn loyalty points for completed order {OrderId}", orderId);
        }
    }

    private AdminOrderDto ToDetailDto(Order order)
    {
        var items = dbContext.OrderItems
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.SkuSnapshot)
            .ThenBy(item => item.Id)
            .ToArray();
        var statusHistory = dbContext.OrderStatusHistories
            .Where(history => history.OrderId == order.Id)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .ToArray();

        return new AdminOrderDto(
            order.Id,
            order.OrderCode,
            order.CustomerId,
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
            items.Select(ToItemDto).ToArray(),
            statusHistory.Select(ToStatusHistoryDto).ToArray());
    }

    private static AdminOrderListItemDto ToListItemDto(Order order, int itemCount)
    {
        return new AdminOrderListItemDto(
            order.Id,
            order.OrderCode,
            order.CustomerName,
            order.CustomerPhone,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.PaymentStatus,
            order.PaidAt,
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

    private static AdminOrderStatusHistoryDto ToStatusHistoryDto(OrderStatusHistory history)
    {
        return new AdminOrderStatusHistoryDto(
            history.Id,
            history.FromStatus,
            history.ToStatus,
            history.Note,
            history.ChangedBy,
            history.ChangedAt);
    }

    private static bool MatchesSearch(Order order, string search)
    {
        return order.OrderCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            order.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            order.CustomerPhone.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
