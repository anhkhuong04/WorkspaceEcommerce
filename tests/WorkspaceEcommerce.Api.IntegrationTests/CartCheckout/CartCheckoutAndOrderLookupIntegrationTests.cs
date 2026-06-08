using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.CartCheckout;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CartCheckoutAndOrderLookupIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task CartCheckoutAndOrderLookup_WithSeededCatalog_CompletesGuestOrderFlow()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        var sessionId = $"integration-cart-{Guid.NewGuid():N}";

        using var addCartResponse = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId,
                productVariantId = catalog.Variant.Id,
                quantity = 2
            });
        var cartJson = await addCartResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, addCartResponse.StatusCode);
        Assert.True(cartJson["success"]!.GetValue<bool>());
        Assert.Equal(2, cartJson["data"]!["totalQuantity"]!.GetValue<int>());
        Assert.Equal(246.90m, cartJson["data"]!["totalAmount"]!.GetValue<decimal>());

        using var checkoutResponse = await client.PostAsJsonAsync(
            "/api/checkout",
            new
            {
                sessionId,
                customerName = "Nguyen Van A",
                customerPhone = "0900000000",
                customerEmail = "customer@example.com",
                shippingAddress = "123 Shipping Street",
                note = "Call before delivery",
                paymentMethod = 0
            });
        var checkoutJson = await checkoutResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        Assert.True(checkoutJson["success"]!.GetValue<bool>());
        var orderCode = checkoutJson["data"]!["order"]!["orderCode"]!.GetValue<string>();
        Assert.StartsWith("ORD-", orderCode, StringComparison.Ordinal);
        Assert.Equal(246.90m, checkoutJson["data"]!["order"]!["totalAmount"]!.GetValue<decimal>());

        var stockQuantity = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.ProductVariants
                .Where(variant => variant.Id == catalog.Variant.Id)
                .Select(variant => variant.StockQuantity)
                .SingleAsync());
        var cartCount = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Carts.CountAsync(cart => cart.SessionId == sessionId));

        Assert.Equal(8, stockQuantity);
        Assert.Equal(0, cartCount);

        using var lookupResponse = await client.GetAsync($"/api/orders/lookup?orderCode={orderCode}&phone=0900000000");
        var lookupJson = await lookupResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        Assert.True(lookupJson["success"]!.GetValue<bool>());
        Assert.Equal(orderCode, lookupJson["data"]!["order"]!["orderCode"]!.GetValue<string>());

        using var wrongPhoneLookupResponse = await client.GetAsync($"/api/orders/lookup?orderCode={orderCode}&phone=0900000001");

        Assert.Equal(HttpStatusCode.NotFound, wrongPhoneLookupResponse.StatusCode);
    }
}
