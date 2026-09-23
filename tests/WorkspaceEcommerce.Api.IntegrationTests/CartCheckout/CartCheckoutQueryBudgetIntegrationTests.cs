using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Api.IntegrationTests.CartCheckout;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CartCheckoutQueryBudgetIntegrationTests(ApiIntegrationTestFixture fixture)
{
    private const int CartReadSelectBudget = 3;
    private const int CheckoutSelectBudget = 5;
    private const int VnPayCheckoutSelectBudget = 8;

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public async Task GetCart_UsesFixedSelectBudgetIndependentOfItemCount(int itemCount)
    {
        await fixture.ResetDatabaseAsync();
        var sessionId = $"cart-query-budget-{itemCount}-{Guid.NewGuid():N}";
        await SeedCartAsync(sessionId, itemCount);
        using var client = fixture.CreateClient();

        fixture.ResetSqlCommandCount();
        using var response = await client.GetAsync($"/api/cart?sessionId={sessionId}");
        var selectCount = fixture.GetSqlSelectCount();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(itemCount, json["data"]!["items"]!.AsArray().Count);
        Assert.Equal(CartReadSelectBudget, selectCount);
    }

    [Theory]
    [InlineData(PaymentMethod.Cod, 1, CheckoutSelectBudget)]
    [InlineData(PaymentMethod.Cod, 20, CheckoutSelectBudget)]
    [InlineData(PaymentMethod.ManualBankTransfer, 1, CheckoutSelectBudget)]
    [InlineData(PaymentMethod.ManualBankTransfer, 20, CheckoutSelectBudget)]
    [InlineData(PaymentMethod.VNPay, 1, VnPayCheckoutSelectBudget)]
    [InlineData(PaymentMethod.VNPay, 20, VnPayCheckoutSelectBudget)]
    public async Task Checkout_UsesFixedSelectBudgetIndependentOfItemCount(
        PaymentMethod paymentMethod,
        int itemCount,
        int expectedSelectCount)
    {
        await fixture.ResetDatabaseAsync();
        var sessionId = $"checkout-query-budget-{paymentMethod}-{itemCount}-{Guid.NewGuid():N}";
        await SeedCartAsync(sessionId, itemCount);
        using var client = fixture.CreateClient();

        fixture.ResetSqlCommandCount();
        using var response = await client.PostAsJsonAsync(
            "/api/checkout",
            CreateCheckoutRequest(sessionId, paymentMethod));
        var selectCount = fixture.GetSqlSelectCount();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(expectedSelectCount, selectCount);
    }

    private async Task SeedCartAsync(string sessionId, int itemCount)
    {
        var category = new Category(
            Guid.NewGuid(),
            null,
            LocalizedText.Of("Query budget"),
            $"query-budget-{Guid.NewGuid():N}",
            1,
            isActive: true);
        var cart = new CartAggregate(Guid.NewGuid(), null, sessionId);
        var products = new List<Product>(itemCount);
        var variants = new List<ProductVariant>(itemCount);

        for (var index = 0; index < itemCount; index++)
        {
            var product = new Product(
                Guid.NewGuid(),
                category.Id,
                LocalizedText.Of($"Product {index}"),
                $"query-product-{Guid.NewGuid():N}",
                null,
                isActive: true);
            product.AddImage(
                Guid.NewGuid(),
                $"https://example.test/query-product-{index}.jpg",
                $"Product {index}",
                1);
            var variant = new ProductVariant(
                Guid.NewGuid(),
                product.Id,
                $"QUERY-{Guid.NewGuid():N}",
                $"Variant {index}",
                null,
                null,
                100_000m + index,
                null,
                100,
                requiresInstallation: false,
                isActive: true,
                weightKg: 1m,
                lengthCm: 10m,
                widthCm: 10m,
                heightCm: 10m);

            cart.AddItem(Guid.NewGuid(), variant.Id, 1, variant.Price);
            products.Add(product);
            variants.Add(variant);
        }

        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Add(category);
            dbContext.AddRange(products);
            dbContext.AddRange(variants);
            dbContext.Add(cart);
            return Task.CompletedTask;
        });
    }

    private static object CreateCheckoutRequest(string sessionId, PaymentMethod paymentMethod) => new
    {
        sessionId,
        customerName = "Query Budget Customer",
        customerPhone = "0900000000",
        customerEmail = "query-budget@example.com",
        shippingAddress = "123 Query Budget Street",
        shippingStreet = "123 Query Budget Street",
        shippingWard = "Ward 1",
        shippingProvince = "Ho Chi Minh",
        note = "Query budget verification",
        paymentMethod = (int)paymentMethod
    };
}
