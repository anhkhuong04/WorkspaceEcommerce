using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Admin.Dashboard;

public sealed class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ExistingData_ReturnsDashboardContract()
    {
        var dbContext = new FakeAppDbContext();
        var category = new Category(Guid.NewGuid(), null, "Desks", "desks", 1);
        var product = new Product(Guid.NewGuid(), category.Id, "Standing Desk", "standing-desk", null);
        var lowStockVariant = new ProductVariant(
            Guid.NewGuid(),
            product.Id,
            "DESK-LOW",
            "Low stock",
            null,
            null,
            100m,
            null,
            3,
            false);
        var healthyVariant = new ProductVariant(
            Guid.NewGuid(),
            product.Id,
            "DESK-OK",
            "Healthy stock",
            null,
            null,
            100m,
            null,
            12,
            false);
        var pendingOrder = CreateOrder("ORD-PENDING-0001");
        var completedOrder = CreateOrder("ORD-COMPLETED-0001");
        MoveToCompleted(completedOrder);
        dbContext.Seed(category);
        dbContext.Seed(product);
        dbContext.Seed(lowStockVariant, healthyVariant);
        dbContext.Seed(pendingOrder, completedOrder);
        var service = new AdminDashboardService(dbContext);

        var result = await service.GetDashboardAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.TotalOrders);
        Assert.Equal(100m, result.Value.TotalRevenue);
        Assert.Equal(1, result.Value.NewOrders);
        Assert.Equal(5, result.Value.LowStockThreshold);
        var lowStock = Assert.Single(result.Value.LowStockVariants);
        Assert.Equal(lowStockVariant.Id, lowStock.VariantId);
        Assert.Equal("Standing Desk", lowStock.ProductName);
        Assert.Equal(3, lowStock.StockQuantity);
        Assert.Equal(7, result.Value.OrderStatusSummary.Count);
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Pending));
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Completed));
        Assert.Equal(0, GetStatusCount(result.Value, OrderStatus.Cancelled));
        var expectedRecentOrderIds = new[] { pendingOrder, completedOrder }
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Select(order => order.Id);
        Assert.Equal(expectedRecentOrderIds, result.Value.RecentOrders.Select(order => order.Id));
        var completedRecentOrder = Assert.Single(
            result.Value.RecentOrders,
            order => order.Status == OrderStatus.Completed);
        Assert.Equal(100m, completedRecentOrder.TotalAmount);
    }

    [Fact]
    public async Task GetDashboardAsync_NonCompletedOrders_DoNotContributeToRevenue()
    {
        var dbContext = new FakeAppDbContext();
        var pendingOrder = CreateOrder("ORD-PENDING-0001");
        var cancelledOrder = CreateOrder("ORD-CANCELLED-0001");
        cancelledOrder.ChangeStatus(Guid.NewGuid(), OrderStatus.Cancelled, "Cancelled", "admin@example.com");
        dbContext.Seed(pendingOrder, cancelledOrder);
        var service = new AdminDashboardService(dbContext);

        var result = await service.GetDashboardAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0m, result.Value.TotalRevenue);
        Assert.Equal(1, result.Value.NewOrders);
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Pending));
        Assert.Equal(1, GetStatusCount(result.Value, OrderStatus.Cancelled));
    }

    [Fact]
    public async Task GetDashboardAsync_EmptyData_ReturnsZeroedContract()
    {
        var service = new AdminDashboardService(new FakeAppDbContext());

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

    [Fact]
    public async Task GetDashboardAsync_MoreThanFiveOrders_ReturnsFiveMostRecentOrders()
    {
        var dbContext = new FakeAppDbContext();
        var orders = Enumerable.Range(1, 6)
            .Select(index => CreateOrder($"ORD-RECENT-{index:0000}"))
            .ToArray();
        var expectedRecentOrderIds = orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Take(5)
            .Select(order => order.Id)
            .ToArray();
        dbContext.Seed(orders);
        var service = new AdminDashboardService(dbContext);

        var result = await service.GetDashboardAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5, result.Value.RecentOrders.Count);
        Assert.Equal(expectedRecentOrderIds, result.Value.RecentOrders.Select(order => order.Id));
    }

    private static int GetStatusCount(AdminDashboardDto dashboard, OrderStatus status)
    {
        return Assert.Single(dashboard.OrderStatusSummary, item => item.Status == status).Count;
    }

    private static Order CreateOrder(string orderCode)
    {
        var order = new Order(
            Guid.NewGuid(),
            orderCode,
            null,
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "123 Shipping Street",
            null,
            PaymentMethod.Cod);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 100m, 1, false);

        return order;
    }

    private static void MoveToCompleted(Order order)
    {
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, "Confirmed", "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, "Processing", "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, "Shipping", "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, "Completed", "admin@example.com");
    }
}
