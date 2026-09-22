using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Ordering;

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

        using var receiptResponse = await client.GetAsync($"/api/admin/orders/{order.Id}/receipt");
        var receipt = await receiptResponse.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, receiptResponse.StatusCode);
        Assert.Equal("application/pdf", receiptResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(receipt, 0, 4));

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new
            {
                status = 1,
                internalNote = "Confirmed by integration test"
            });
        var updateJson = await updateResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(updateJson["success"]!.GetValue<bool>());
        Assert.Equal(1, updateJson["data"]!["status"]!.GetValue<int>());
        Assert.Equal(2, updateJson["data"]!["statusHistory"]!.AsArray().Count);
        var latestHistory = updateJson["data"]!["statusHistory"]![1]!;
        Assert.Equal(0, latestHistory["fromStatus"]!.GetValue<int>());
        Assert.Equal(1, latestHistory["toStatus"]!.GetValue<int>());
        Assert.Equal("Confirmed by integration test", latestHistory["internalNote"]!.GetValue<string>());
        Assert.Equal("admin@example.com", latestHistory["changedBy"]!.GetValue<string>());
    }

    [Fact]
    public async Task UpdateOrderStatus_InvalidTransition_ReturnsConflictEnvelope()
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

        using var response = await client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new
            {
                status = 4,
                internalNote = "Invalid direct completion"
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Contains(
            "Order status cannot change from Pending to Completed.",
            json["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }

    [Fact]
    public async Task CancelOrder_RequiresReasonAndPersistsSeparatedMessagesAndStockRestore()
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

        using var invalidResponse = await client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new { status = (int)OrderStatus.Cancelled, internalNote = "Private operations note" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        using var response = await client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new
            {
                status = (int)OrderStatus.Cancelled,
                cancellationReason = "Product is unavailable",
                customerMessage = "We apologize for the inconvenience.",
                internalNote = "Private operations note"
            });
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((int)OrderStatus.Cancelled, json["data"]!["status"]!.GetValue<int>());
        Assert.Equal((int)PaymentStatus.Cancelled, json["data"]!["paymentStatus"]!.GetValue<int>());
        var history = json["data"]!["statusHistory"]!.AsArray().Last()!;
        Assert.Equal("Product is unavailable", history["cancellationReason"]!.GetValue<string>());
        Assert.Equal("We apologize for the inconvenience.", history["customerMessage"]!.GetValue<string>());
        Assert.Equal("Private operations note", history["internalNote"]!.GetValue<string>());

        var stockQuantity = await fixture.ExecuteDbAsync(dbContext => dbContext.ProductVariants
            .Where(variant => variant.Id == catalog.Variant.Id)
            .Select(variant => variant.StockQuantity)
            .SingleAsync());
        Assert.Equal(12, stockQuantity);
    }

    [Fact]
    public async Task ListOrders_UsesBoundedCountAndPageQueries()
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
        fixture.ResetSqlCommandCount();

        using var response = await client.GetAsync("/api/admin/orders?pageNumber=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.InRange(fixture.GetSqlSelectCount(), 2, 2);
    }
}
