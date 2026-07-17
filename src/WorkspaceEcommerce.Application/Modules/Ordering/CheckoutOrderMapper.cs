using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal static class CheckoutOrderMapper
{
    public static OrderDto ToDto(Order order)
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
            order.Items.Select(ToDto).ToArray());
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
}
