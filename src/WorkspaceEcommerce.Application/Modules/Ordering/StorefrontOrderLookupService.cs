using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class StorefrontOrderLookupService(
    IAppDbContext dbContext,
    IValidator<OrderLookupRequest> validator) : IStorefrontOrderLookupService
{
    public async Task<Result<OrderLookupResponse>> LookupAsync(
        OrderLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<OrderLookupResponse>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var orderCode = NormalizeOrderCode(request.OrderCode);
        var phone = NormalizePhone(request.Phone);
        var order = dbContext.Orders.FirstOrDefault(existing =>
            existing.OrderCode == orderCode &&
            existing.CustomerPhone == phone);

        if (order is null)
        {
            return Result<OrderLookupResponse>.NotFound("Order was not found.");
        }

        var items = dbContext.OrderItems
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.SkuSnapshot)
            .ThenBy(item => item.Id)
            .ToArray();

        return Result<OrderLookupResponse>.Success(new OrderLookupResponse(ToDto(order, items)));
    }

    private static OrderDto ToDto(Order order, IReadOnlyCollection<OrderItem> items)
    {
        return new OrderDto(
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
            items.Select(ToDto).ToArray());
    }

    private static OrderItemDto ToDto(OrderItem item)
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

    private static string NormalizeOrderCode(string orderCode)
    {
        return orderCode.Trim().ToUpperInvariant();
    }

    private static string NormalizePhone(string phone)
    {
        return phone.Trim();
    }
}
