namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public sealed record AdminDashboardDto(
    int TotalOrders,
    decimal TotalRevenue,
    int NewOrders,
    int LowStockThreshold,
    IReadOnlyCollection<LowStockProductVariantDto> LowStockVariants,
    IReadOnlyCollection<AdminOrderStatusSummaryDto> OrderStatusSummary,
    IReadOnlyCollection<RecentAdminOrderDto> RecentOrders)
{
    public decimal NetCollectedRevenue { get; init; }
    public IReadOnlyCollection<AdminOperationalQueueDto> Operations { get; init; } = [];
}

public sealed record AdminOperationalQueueDto(string Key, string Label, int Count, string Url);
