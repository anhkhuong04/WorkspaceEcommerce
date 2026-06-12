namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public sealed record AdminDashboardDto(
    int TotalOrders,
    decimal TotalRevenue,
    int NewOrders,
    int LowStockThreshold,
    IReadOnlyCollection<LowStockProductVariantDto> LowStockVariants,
    IReadOnlyCollection<AdminOrderStatusSummaryDto> OrderStatusSummary,
    IReadOnlyCollection<RecentAdminOrderDto> RecentOrders);
