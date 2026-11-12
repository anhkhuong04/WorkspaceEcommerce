namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public interface IAdminDashboardQuery
{
    Task<decimal> GetNetCollectedRevenueAsync(CancellationToken cancellationToken = default) => Task.FromResult(0m);
    Task<IReadOnlyCollection<AdminOperationalQueueDto>> GetOperationsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AdminOperationalQueueDto>>([]);
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
