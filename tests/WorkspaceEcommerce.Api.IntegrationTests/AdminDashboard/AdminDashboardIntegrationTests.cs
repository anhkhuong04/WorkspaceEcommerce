using System.Net;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminDashboard;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminDashboardIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetDashboard_WithBearerToken_ReturnsBasicMetrics()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        catalog.Variant.UpdateStock(3);
        var pendingOrder = TestData.CreatePendingOrder(catalog.Variant.Id);
        var completedOrder = CreateCompletedOrder(catalog.Variant.Id);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, pendingOrder, completedOrder);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.GetAsync("/api/admin/dashboard");
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal(2, json["data"]!["totalOrders"]!.GetValue<int>());
        Assert.Equal(100m, json["data"]!["totalRevenue"]!.GetValue<decimal>());
        Assert.Equal(1, json["data"]!["newOrders"]!.GetValue<int>());
        var lowStock = Assert.Single(json["data"]!["lowStockVariants"]!.AsArray());
        Assert.Equal("DESK-001", lowStock!["sku"]!.GetValue<string>());
        Assert.Equal(3, lowStock["stockQuantity"]!.GetValue<int>());
    }

    [Fact]
    public async Task GetDashboard_WithoutBearerToken_ReturnsUnauthorized()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Order CreateCompletedOrder(Guid productVariantId)
    {
        var order = new Order(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            "ORD-TEST-0002",
            null,
            "Tran Van B",
            "0900000002",
            "customer2@example.com",
            "456 Shipping Street",
            null,
            PaymentMethod.Cod);
        order.AddItem(
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            productVariantId,
            "Standing Desk",
            "DESK-001",
            100m,
            1,
            requiresInstallation: false);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, "Confirmed", "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, "Processing", "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, "Shipping", "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, "Completed", "admin@example.com");

        return order;
    }
}
