using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Admin.Dashboard;

public sealed class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_QueryResults_ReturnsDashboardContract()
    {
        var lowStockVariant = new LowStockProductVariantDto(
            Guid.NewGuid(),
            "Standing Desk",
            Guid.NewGuid(),
            "DESK-LOW",
            "Low stock",
            3,
            true);
        var pendingOrder = CreateRecentOrder("ORD-PENDING-0001", OrderStatus.Pending);
        var completedOrder = CreateRecentOrder("ORD-COMPLETED-0001", OrderStatus.Completed);
        var query = new StubAdminDashboardQuery
        {
            OrderStatusSummary =
            [
                new AdminOrderStatusSummaryDto(OrderStatus.Pending, 1),
                new AdminOrderStatusSummaryDto(OrderStatus.Completed, 1)
            ],
            CompletedRevenue = 100m,
            LowStockVariants = [lowStockVariant],
            RecentOrders = [completedOrder, pendingOrder]
        };
        var service = new AdminDashboardService(query);

        var result = await service.GetDashboardAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.TotalOrders);
        Assert.Equal(100m, result.Value.TotalRevenue);
        Assert.Equal(1, result.Value.NewOrders);
        Assert.Equal(5, result.Value.LowStockThreshold);
        Assert.Same(lowStockVariant, Assert.Single(result.Value.LowStockVariants));
        Assert.Equal(7, result.Value.OrderStatusSummary.Count);
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Pending));
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Completed));
        Assert.Equal(0, GetStatusCount(result.Value, OrderStatus.Cancelled));
        Assert.Equal(query.RecentOrders, result.Value.RecentOrders);
        Assert.Equal(5, query.ReceivedLowStockThreshold);
        Assert.Equal(10, query.ReceivedLowStockLimit);
        Assert.Equal(5, query.ReceivedRecentOrderLimit);
    }

    [Fact]
    public async Task GetDashboardAsync_NonCompletedStatusCounts_DoNotChangeCompletedRevenue()
    {
        var query = new StubAdminDashboardQuery
        {
            OrderStatusSummary =
            [
                new AdminOrderStatusSummaryDto(OrderStatus.Pending, 1),
                new AdminOrderStatusSummaryDto(OrderStatus.Cancelled, 1)
            ],
            CompletedRevenue = 0m
        };
        var service = new AdminDashboardService(query);

        var result = await service.GetDashboardAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.TotalOrders);
        Assert.Equal(0m, result.Value.TotalRevenue);
        Assert.Equal(1, result.Value.NewOrders);
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Pending));
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Cancelled));
    }

    [Fact]
    public async Task GetDashboardAsync_EmptyData_ReturnsZeroedContract()
    {
        var service = new AdminDashboardService(new StubAdminDashboardQuery());

        var result = await service.GetDashboardAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value.TotalOrders);
        Assert.Equal(0m, result.Value.TotalRevenue);
        Assert.Equal(0, result.Value.NewOrders);
        Assert.Equal(5, result.Value.LowStockThreshold);
        Assert.Empty(result.Value.LowStockVariants);
        Assert.Equal(7, result.Value.OrderStatusSummary.Count);
        Assert.All(result.Value.OrderStatusSummary, item => Assert.Equal(0, item.Count));
        Assert.Empty(result.Value.RecentOrders);
    }

    private static int GetStatusCount(AdminDashboardDto dashboard, OrderStatus status)
    {
        return Assert.Single(dashboard.OrderStatusSummary, item => item.Status == status).Count;
    }

    private static RecentAdminOrderDto CreateRecentOrder(string orderCode, OrderStatus status)
    {
        return new RecentAdminOrderDto(
            Guid.NewGuid(),
            orderCode,
            "Nguyen Van A",
            100m,
            status,
            DateTimeOffset.UtcNow);
    }

    private sealed class StubAdminDashboardQuery : IAdminDashboardQuery
    {
        public IReadOnlyCollection<AdminOrderStatusSummaryDto> OrderStatusSummary { get; init; } = [];

        public decimal CompletedRevenue { get; init; }

        public IReadOnlyCollection<LowStockProductVariantDto> LowStockVariants { get; init; } = [];

        public IReadOnlyCollection<RecentAdminOrderDto> RecentOrders { get; init; } = [];

        public int ReceivedLowStockThreshold { get; private set; }

        public int ReceivedLowStockLimit { get; private set; }

        public int ReceivedRecentOrderLimit { get; private set; }

        public Task<IReadOnlyCollection<AdminOrderStatusSummaryDto>> GetOrderStatusSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OrderStatusSummary);
        }

        public Task<decimal> GetCompletedRevenueAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CompletedRevenue);
        }

        public Task<IReadOnlyCollection<LowStockProductVariantDto>> GetLowStockVariantsAsync(
            int threshold,
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedLowStockThreshold = threshold;
            ReceivedLowStockLimit = limit;
            return Task.FromResult(LowStockVariants);
        }

        public Task<IReadOnlyCollection<RecentAdminOrderDto>> GetRecentOrdersAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedRecentOrderLimit = limit;
            return Task.FromResult(RecentOrders);
        }
    }
}
