using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

public sealed record CustomerOrderListItemDto(
    Guid Id,
    string OrderCode,
    decimal TotalAmount,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int ItemCount);
