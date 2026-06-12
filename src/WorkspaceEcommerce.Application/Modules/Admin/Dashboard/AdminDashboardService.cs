using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

internal sealed class AdminDashboardService(IAdminDashboardQuery dashboardQuery) : IAdminDashboardService
{
    private const int LowStockThreshold = 5;
    private const int LowStockLimit = 10;
    private const int RecentOrderLimit = 5;

    public async Task<Result<AdminDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var persistedStatusSummary = await dashboardQuery.GetOrderStatusSummaryAsync(cancellationToken);
        var statusCounts = persistedStatusSummary.ToDictionary(item => item.Status, item => item.Count);
        var orderStatusSummary = Enum.GetValues<OrderStatus>()
            .Select(status => new AdminOrderStatusSummaryDto(
                status,
                statusCounts.GetValueOrDefault(status)))
            .ToArray();
        var totalRevenue = await dashboardQuery.GetCompletedRevenueAsync(cancellationToken);
        var lowStockVariants = await dashboardQuery.GetLowStockVariantsAsync(
            LowStockThreshold,
            LowStockLimit,
            cancellationToken);
        var recentOrders = await dashboardQuery.GetRecentOrdersAsync(
            RecentOrderLimit,
            cancellationToken);

        var dashboard = new AdminDashboardDto(
            orderStatusSummary.Sum(item => item.Count),
            totalRevenue,
            statusCounts.GetValueOrDefault(OrderStatus.Pending),
            LowStockThreshold,
            lowStockVariants,
            orderStatusSummary,
            recentOrders);

        return Result<AdminDashboardDto>.Success(dashboard);
    }
}
