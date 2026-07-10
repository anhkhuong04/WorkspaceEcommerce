using System.Net;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminDashboard;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminDashboardIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetDashboard_WithBearerToken_ReturnsDashboardContract()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        catalog.Variant.UpdateStock(3);
        var outOfStockVariant = new ProductVariant(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            catalog.Product.Id,
            "A-OUT",
            "Out of stock",
            null,
            null,
            100m,
            null,
            0,
            false);
        var thresholdVariant = new ProductVariant(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            catalog.Product.Id,
            "B-THRESHOLD",
            "Threshold stock",
            null,
            null,
            100m,
            null,
            5,
            false);
        var healthyVariant = new ProductVariant(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            catalog.Product.Id,
            "C-HEALTHY",
            "Healthy stock",
            null,
            null,
            100m,
            null,
            6,
            false);
        var pendingOrder = TestData.CreatePendingOrder(catalog.Variant.Id);
        var completedOrder = CreateCompletedOrder(catalog.Variant.Id);
        var additionalPendingOrders = Enumerable.Range(1, 4)
            .Select(index => CreatePendingOrder(index, catalog.Variant.Id))
            .ToArray();
        var orders = new[] { pendingOrder, completedOrder }
            .Concat(additionalPendingOrders)
            .ToArray();
        var expectedRecentOrderIds = orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Take(5)
            .Select(order => order.Id.ToString())
            .ToArray();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(
                catalog.Category,
                catalog.Product,
                catalog.Variant,
                outOfStockVariant,
                thresholdVariant,
                healthyVariant);
            dbContext.AddRange(orders);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.GetAsync("/api/admin/dashboard");
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal(6, json["data"]!["totalOrders"]!.GetValue<int>());
        Assert.Equal(100m, json["data"]!["totalRevenue"]!.GetValue<decimal>());
        Assert.Equal(5, json["data"]!["newOrders"]!.GetValue<int>());
        Assert.Equal(5, json["data"]!["lowStockThreshold"]!.GetValue<int>());
        var lowStock = json["data"]!["lowStockVariants"]!.AsArray();
        Assert.Equal(3, lowStock.Count);
        Assert.Equal(
            ["A-OUT", "DESK-001", "B-THRESHOLD"],
            lowStock.Select(item => item!["sku"]!.GetValue<string>()));
        Assert.DoesNotContain(lowStock, item => item!["sku"]!.GetValue<string>() == "C-HEALTHY");
        var statusSummary = json["data"]!["orderStatusSummary"]!.AsArray();
        Assert.Equal(8, statusSummary.Count);
        Assert.Equal(5, GetStatusCount(statusSummary, OrderStatus.Pending));
        Assert.Equal(1, GetStatusCount(statusSummary, OrderStatus.Completed));
        Assert.Equal(0, GetStatusCount(statusSummary, OrderStatus.Cancelled));
        var recentOrders = json["data"]!["recentOrders"]!.AsArray();
        Assert.Equal(5, recentOrders.Count);
        Assert.Equal(
            expectedRecentOrderIds,
            recentOrders.Select(item => item!["id"]!.GetValue<string>()));
    }

    [Fact]
    public async Task GetDashboard_WithoutBearerToken_ReturnsUnauthorized()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_EmptyData_ReturnsZeroedContract()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.GetAsync("/api/admin/dashboard");
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, json["data"]!["totalOrders"]!.GetValue<int>());
        Assert.Equal(0m, json["data"]!["totalRevenue"]!.GetValue<decimal>());
        Assert.Equal(0, json["data"]!["newOrders"]!.GetValue<int>());
        Assert.Equal(5, json["data"]!["lowStockThreshold"]!.GetValue<int>());
        Assert.Empty(json["data"]!["lowStockVariants"]!.AsArray());
        Assert.Equal(8, json["data"]!["orderStatusSummary"]!.AsArray().Count);
        Assert.All(
            json["data"]!["orderStatusSummary"]!.AsArray(),
            item => Assert.Equal(0, item!["count"]!.GetValue<int>()));
        Assert.Empty(json["data"]!["recentOrders"]!.AsArray());
    }

    private static int GetStatusCount(System.Text.Json.Nodes.JsonArray summary, OrderStatus status)
    {
        return summary
            .Single(item => item!["status"]!.GetValue<int>() == (int)status)!["count"]!
            .GetValue<int>();
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
            PaymentMethod.Cod,
            "USD",
            1m);
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

    private static Order CreatePendingOrder(int index, Guid productVariantId)
    {
        var order = new Order(
            Guid.Parse($"{index + 3:D8}-4444-4444-4444-444444444444"),
            $"ORD-RECENT-{index:0000}",
            null,
            $"Customer {index}",
            $"09000000{index:00}",
            $"customer{index}@example.com",
            $"{index} Shipping Street",
            null,
            PaymentMethod.Cod,
            "USD",
            1m);
        order.AddItem(
            Guid.Parse($"{index + 3:D8}-5555-5555-5555-555555555555"),
            productVariantId,
            "Standing Desk",
            "DESK-001",
            100m,
            1,
            requiresInstallation: false);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);

        return order;
    }
}
