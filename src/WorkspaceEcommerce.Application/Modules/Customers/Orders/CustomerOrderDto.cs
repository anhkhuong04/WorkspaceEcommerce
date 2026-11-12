using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

public sealed record CustomerOrderDto(
    Guid Id,
    string OrderCode,
    Guid CustomerId,
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
    PaymentStatus PaymentStatus,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? TrackingCode,
    Guid? ShipmentId,
    IReadOnlyCollection<OrderItemDto> Items,
    IReadOnlyCollection<CustomerOrderStatusHistoryDto> StatusHistory)
{
    public DateTimeOffset? PaymentDueAt { get; init; }
    public Shipments.OrderShippingDetailsDto? Shipping { get; init; }
    public DateTimeOffset? ReturnRequestedAt { get; init; }
    public DateTimeOffset? ReturnReviewedAt { get; init; }
    public string? ReturnDecision { get; init; }
    public string? ReturnDecisionReason { get; init; }
    public decimal RefundedAmount { get; init; }
    public string? ReturnReason { get; init; }
    public DateTimeOffset? ReturnRequestDeadline { get; init; }
    public bool CanRequestReturn { get; init; }
}
