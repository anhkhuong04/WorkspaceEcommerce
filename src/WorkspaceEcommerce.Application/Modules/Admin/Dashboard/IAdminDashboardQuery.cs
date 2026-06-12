namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public interface IAdminDashboardQuery
{
    Task<IReadOnlyCollection<AdminOrderStatusSummaryDto>> GetOrderStatusSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<decimal> GetCompletedRevenueAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LowStockProductVariantDto>> GetLowStockVariantsAsync(
        int threshold,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RecentAdminOrderDto>> GetRecentOrdersAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
