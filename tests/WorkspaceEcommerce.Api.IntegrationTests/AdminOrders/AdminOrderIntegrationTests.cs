using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminOrders;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminOrderIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task AdminOrderEndpoints_WithBearerToken_ListDetailAndUpdateStatus()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        var order = TestData.CreatePendingOrder(catalog.Variant.Id);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, order);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var listResponse = await client.GetAsync("/api/admin/orders?pageNumber=1&pageSize=10&search=ORD-TEST");
        var listJson = await listResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.True(listJson["success"]!.GetValue<bool>());
        Assert.Equal(1, listJson["data"]!["totalCount"]!.GetValue<int>());
        Assert.Equal("ORD-TEST-0001", listJson["data"]!["items"]![0]!["orderCode"]!.GetValue<string>());

        using var detailResponse = await client.GetAsync($"/api/admin/orders/{order.Id}");
        var detailJson = await detailResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.True(detailJson["success"]!.GetValue<bool>());
        Assert.Equal("DESK-001", detailJson["data"]!["items"]![0]!["skuSnapshot"]!.GetValue<string>());
        Assert.Single(detailJson["data"]!["statusHistory"]!.AsArray());

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new
            {
                status = 1,
                note = "Confirmed by integration test"
            });
        var updateJson = await updateResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(updateJson["success"]!.GetValue<bool>());
        Assert.Equal(1, updateJson["data"]!["status"]!.GetValue<int>());
        Assert.Equal(2, updateJson["data"]!["statusHistory"]!.AsArray().Count);
        var latestHistory = updateJson["data"]!["statusHistory"]![1]!;
        Assert.Equal(0, latestHistory["fromStatus"]!.GetValue<int>());
        Assert.Equal(1, latestHistory["toStatus"]!.GetValue<int>());
        Assert.Equal("Confirmed by integration test", latestHistory["note"]!.GetValue<string>());
        Assert.Equal("admin@example.com", latestHistory["changedBy"]!.GetValue<string>());
    }
}
