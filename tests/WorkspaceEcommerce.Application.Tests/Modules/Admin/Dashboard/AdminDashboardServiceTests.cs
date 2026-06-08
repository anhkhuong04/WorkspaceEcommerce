using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Admin.Dashboard;

public sealed class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ExistingData_ReturnsBasicMetricsAndLowStockVariants()
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
        var lowStock = Assert.Single(result.Value.LowStockVariants);
        Assert.Equal(lowStockVariant.Id, lowStock.VariantId);
        Assert.Equal("Standing Desk", lowStock.ProductName);
        Assert.Equal(3, lowStock.StockQuantity);
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
