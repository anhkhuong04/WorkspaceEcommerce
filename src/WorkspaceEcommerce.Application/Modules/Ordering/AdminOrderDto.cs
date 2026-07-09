using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed record AdminOrderDto(
    Guid Id,
    string OrderCode,
    Guid? CustomerId,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string ShippingAddress,
    string? Note,
    Guid? CouponId,
    string? CouponCodeSnapshot,
    string? CouponNameSnapshot,
    decimal Subtotal,
    decimal ShippingFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? TrackingCode,
    Guid? ShipmentId,
    IReadOnlyCollection<OrderItemDto> Items,
    IReadOnlyCollection<AdminOrderStatusHistoryDto> StatusHistory);
