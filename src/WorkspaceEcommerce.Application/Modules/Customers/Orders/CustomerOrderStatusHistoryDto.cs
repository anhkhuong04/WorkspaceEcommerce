using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

public sealed record CustomerOrderStatusHistoryDto(
    Guid Id,
    OrderStatus? FromStatus,
    OrderStatus ToStatus,
    string? CancellationReason,
    string? CustomerMessage,
    DateTimeOffset ChangedAt);
