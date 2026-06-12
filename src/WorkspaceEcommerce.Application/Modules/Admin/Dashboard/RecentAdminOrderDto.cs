using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public sealed record RecentAdminOrderDto(
    Guid Id,
    string OrderCode,
    string CustomerName,
    decimal TotalAmount,
    OrderStatus Status,
    DateTimeOffset CreatedAt);
