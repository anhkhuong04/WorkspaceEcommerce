using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public sealed record AdminOrderStatusSummaryDto(
    OrderStatus Status,
    int Count);
