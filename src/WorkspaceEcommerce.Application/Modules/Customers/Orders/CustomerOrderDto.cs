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
    decimal Subtotal,
    decimal ShippingFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<OrderItemDto> Items,
    IReadOnlyCollection<CustomerOrderStatusHistoryDto> StatusHistory);
