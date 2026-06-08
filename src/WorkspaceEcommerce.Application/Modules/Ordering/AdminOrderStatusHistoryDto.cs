using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed record AdminOrderStatusHistoryDto(
    Guid Id,
    OrderStatus? FromStatus,
    OrderStatus ToStatus,
    string? Note,
    string? ChangedBy,
    DateTimeOffset ChangedAt);
