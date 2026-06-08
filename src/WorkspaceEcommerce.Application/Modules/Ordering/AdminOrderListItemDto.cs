using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed record AdminOrderListItemDto(
    Guid Id,
    string OrderCode,
    string CustomerName,
    string CustomerPhone,
    decimal TotalAmount,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int ItemCount);
