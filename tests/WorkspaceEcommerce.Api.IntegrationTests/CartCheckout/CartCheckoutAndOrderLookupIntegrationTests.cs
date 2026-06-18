using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Coupons;

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

    [Fact]
    public async Task Checkout_WithCustomerToken_LinksOrderToAuthenticatedCustomer()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        var token = await client.RegisterCustomerAsync();
        client.UseBearerToken(token);
        var customerId = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Customers
                .Where(customer => customer.Email == "customer@example.com")
                .Select(customer => customer.Id)
                .SingleAsync());
        var sessionId = $"authenticated-checkout-{Guid.NewGuid():N}";
        using var addCartResponse = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId,
                productVariantId = catalog.Variant.Id,
                quantity = 1
            });
        addCartResponse.EnsureSuccessStatusCode();

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
        Assert.Equal(customerId, checkoutJson["data"]!["order"]!["customerId"]!.GetValue<Guid>());
        var orderId = checkoutJson["data"]!["order"]!["id"]!.GetValue<Guid>();
        var persistedCustomerId = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Orders
                .Where(order => order.Id == orderId)
                .Select(order => order.CustomerId)
                .SingleAsync());

        Assert.Equal(customerId, persistedCustomerId);

        using var customerOrdersResponse = await client.GetAsync("/api/customer/orders");
        var customerOrdersJson = await customerOrdersResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, customerOrdersResponse.StatusCode);
        var item = Assert.Single(customerOrdersJson["data"]!["items"]!.AsArray());
        Assert.Equal(orderId, item!["id"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task ValidateCheckoutCoupon_WithValidCoupon_ReturnsDiscountPreview()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        var coupon = CreateCoupon("SAVE10", CouponDiscountType.Percentage, 10m);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, coupon);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        var sessionId = $"coupon-preview-{Guid.NewGuid():N}";
        using var addCartResponse = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId,
                productVariantId = catalog.Variant.Id,
                quantity = 2
            });
        addCartResponse.EnsureSuccessStatusCode();

        using var validateResponse = await client.PostAsJsonAsync(
            "/api/checkout/coupons/validate",
            new
            {
                sessionId,
                couponCode = " save10 "
            });
        var validateJson = await validateResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);
        Assert.True(validateJson["success"]!.GetValue<bool>());
        Assert.Equal("SAVE10", validateJson["data"]!["couponCode"]!.GetValue<string>());
        Assert.Equal(246.90m, validateJson["data"]!["subtotal"]!.GetValue<decimal>());
        Assert.Equal(246.90m, validateJson["data"]!["eligibleSubtotal"]!.GetValue<decimal>());
        Assert.Equal(24.69m, validateJson["data"]!["discountAmount"]!.GetValue<decimal>());
        Assert.Equal(222.21m, validateJson["data"]!["totalAmount"]!.GetValue<decimal>());

        var usedCount = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Coupons
                .Where(existing => existing.Id == coupon.Id)
                .Select(existing => existing.UsedCount)
                .SingleAsync());

        Assert.Equal(0, usedCount);
    }

    [Fact]
    public async Task Checkout_WithCoupon_AppliesDiscountCreatesRedemptionAndIncrementsUsage()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        var coupon = CreateCoupon("SAVE20", CouponDiscountType.FixedAmount, 20m);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, coupon);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        var sessionId = $"coupon-checkout-{Guid.NewGuid():N}";
        using var addCartResponse = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId,
                productVariantId = catalog.Variant.Id,
                quantity = 2
            });
        addCartResponse.EnsureSuccessStatusCode();

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
                paymentMethod = 0,
                couponCode = "save20"
            });
        var checkoutJson = await checkoutResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        Assert.True(checkoutJson["success"]!.GetValue<bool>());
        var order = checkoutJson["data"]!["order"]!;
        var orderId = order["id"]!.GetValue<Guid>();
        Assert.Equal(coupon.Id, order["couponId"]!.GetValue<Guid>());
        Assert.Equal("SAVE20", order["couponCodeSnapshot"]!.GetValue<string>());
        Assert.Equal(20m, order["discountAmount"]!.GetValue<decimal>());
        Assert.Equal(226.90m, order["totalAmount"]!.GetValue<decimal>());

        var persisted = await fixture.ExecuteDbAsync(async dbContext =>
        {
            var persistedOrder = await dbContext.Orders.SingleAsync(existing => existing.Id == orderId);
            var redemption = await dbContext.CouponRedemptions.SingleAsync(existing => existing.OrderId == orderId);
            var usedCount = await dbContext.Coupons
                .Where(existing => existing.Id == coupon.Id)
                .Select(existing => existing.UsedCount)
                .SingleAsync();

            return new
            {
                persistedOrder.CouponId,
                persistedOrder.CouponCodeSnapshot,
                RedemptionCouponId = redemption.CouponId,
                redemption.CodeSnapshot,
                UsedCount = usedCount
            };
        });

        Assert.Equal(coupon.Id, persisted.CouponId);
        Assert.Equal("SAVE20", persisted.CouponCodeSnapshot);
        Assert.Equal(coupon.Id, persisted.RedemptionCouponId);
        Assert.Equal("SAVE20", persisted.CodeSnapshot);
        Assert.Equal(1, persisted.UsedCount);
    }

    [Fact]
    public async Task Checkout_CouponUsageLimitReached_ReturnsConflict()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        var coupon = CreateCoupon("LIMIT1", CouponDiscountType.Percentage, 10m, usageLimit: 1);
        coupon.ReserveUsage();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, coupon);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        var sessionId = $"coupon-limit-{Guid.NewGuid():N}";
        using var addCartResponse = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId,
                productVariantId = catalog.Variant.Id,
                quantity = 1
            });
        addCartResponse.EnsureSuccessStatusCode();

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
                paymentMethod = 0,
                couponCode = "LIMIT1"
            });
        var checkoutJson = await checkoutResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, checkoutResponse.StatusCode);
        Assert.False(checkoutJson["success"]!.GetValue<bool>());
        Assert.Contains(
            "Coupon usage limit has been reached.",
            checkoutJson["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }

    [Fact]
    public async Task AddCartItem_InvalidRequest_ReturnsValidationEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId = "",
                productVariantId = Guid.Empty,
                quantity = 0
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(json["success"]!.GetValue<bool>());
        var errors = json["errors"]!.AsArray().Select(error => error!.GetValue<string>()).ToArray();
        Assert.Contains(errors, error => error.Contains("Session Id", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Product Variant Id", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Quantity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddCartItem_InactiveVariant_ReturnsNotFound()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        catalog.Variant.Deactivate();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId = "inactive-variant-cart",
                productVariantId = catalog.Variant.Id,
                quantity = 1
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Contains(
            "Product variant was not found.",
            json["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }

    [Fact]
    public async Task Checkout_ProductDeactivatedAfterCartAdd_ReturnsNotFoundAndDoesNotDecreaseStock()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        var sessionId = $"inactive-checkout-{Guid.NewGuid():N}";
        using var addCartResponse = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId,
                productVariantId = catalog.Variant.Id,
                quantity = 2
            });
        addCartResponse.EnsureSuccessStatusCode();

        await fixture.ExecuteDbAsync(async dbContext =>
        {
            var product = await dbContext.Products.SingleAsync(existing => existing.Id == catalog.Product.Id);
            product.Deactivate();
            await dbContext.SaveChangesAsync();

            return true;
        });

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

        Assert.Equal(HttpStatusCode.NotFound, checkoutResponse.StatusCode);
        var checkout = await checkoutResponse.ReadJsonAsync();
        Assert.False(checkout["success"]!.GetValue<bool>());
        Assert.Contains(
            "Product variant was not found.",
            checkout["errors"]!.AsArray().Select(error => error!.GetValue<string>()));

        var stockQuantity = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.ProductVariants
                .Where(variant => variant.Id == catalog.Variant.Id)
                .Select(variant => variant.StockQuantity)
                .SingleAsync());

        Assert.Equal(10, stockQuantity);
    }

    private static Coupon CreateCoupon(
        string code,
        CouponDiscountType discountType,
        decimal discountValue,
        int? usageLimit = null)
    {
        return new Coupon(
            Guid.NewGuid(),
            code,
            "Checkout coupon",
            null,
            discountType,
            discountValue,
            null,
            null,
            null,
            null,
            usageLimit);
    }
}
