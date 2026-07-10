using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Api.IntegrationTests.Loyalty;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class LoyaltyIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task LoyaltyManualFlow_CheckoutCompleteEarnRedeemAndUseVoucher()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi");
        var customerToken = await client.RegisterCustomerAsync(
            email: $"loyalty-{Guid.NewGuid():N}@example.com",
            password: "customer-password");
        client.UseBearerToken(customerToken);

        var firstSessionId = $"loyalty-first-{Guid.NewGuid():N}";
        await AddCartItemAsync(client, firstSessionId, catalog.Variant.Id);
        using var firstCheckoutResponse = await client.PostAsJsonAsync(
            "/api/checkout",
            new
            {
                sessionId = firstSessionId,
                customerName = "Nguyen Van A",
                customerPhone = "0900000000",
                customerEmail = "customer@example.com",
                shippingAddress = "123 Shipping Street",
                shippingStreet = "123 Shipping Street",
                shippingWard = "Ward 1",
                shippingProvince = "Ho Chi Minh",
                note = "Call before delivery",
                paymentMethod = 0
            });
        var firstCheckoutJson = await firstCheckoutResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, firstCheckoutResponse.StatusCode);
        var firstOrderId = firstCheckoutJson["data"]!["order"]!["id"]!.GetValue<Guid>();

        var adminToken = await client.LoginAsAdminAsync();
        client.UseBearerToken(adminToken);
        await UpdateOrderStatusAsync(client, firstOrderId, status: 1);
        await UpdateOrderStatusAsync(client, firstOrderId, status: 2);
        await UpdateOrderStatusAsync(client, firstOrderId, status: 3);
        await UpdateOrderStatusAsync(client, firstOrderId, status: 4);

        client.UseBearerToken(customerToken);
        using var loyaltyResponse = await client.GetAsync("/api/loyalty/me");
        var loyaltyJson = await loyaltyResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, loyaltyResponse.StatusCode);
        var currentPoints = loyaltyJson["data"]!["currentPoints"]!.GetValue<int>();
        Assert.True(currentPoints > 0);

        using var redeemResponse = await client.PostAsJsonAsync(
            "/api/loyalty/me/redeem",
            new { points = 1 });
        var redeemJson = await redeemResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, redeemResponse.StatusCode);
        var voucherCode = redeemJson["data"]!["voucherCode"]!.GetValue<string>();
        Assert.StartsWith(Coupon.LoyaltyVoucherCodePrefix, voucherCode, StringComparison.Ordinal);
        Assert.Equal(currentPoints - 1, redeemJson["data"]!["remainingPoints"]!.GetValue<int>());

        var secondSessionId = $"loyalty-second-{Guid.NewGuid():N}";
        await AddCartItemAsync(client, secondSessionId, catalog.Variant.Id);
        using var secondCheckoutResponse = await client.PostAsJsonAsync(
            "/api/checkout",
            new
            {
                sessionId = secondSessionId,
                customerName = "Nguyen Van A",
                customerPhone = "0900000000",
                customerEmail = "customer@example.com",
                shippingAddress = "123 Shipping Street",
                shippingStreet = "123 Shipping Street",
                shippingWard = "Ward 1",
                shippingProvince = "Ho Chi Minh",
                note = "Use loyalty voucher",
                paymentMethod = 0,
                couponCode = voucherCode
            });
        var secondCheckoutJson = await secondCheckoutResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, secondCheckoutResponse.StatusCode);
        Assert.Equal(voucherCode, secondCheckoutJson["data"]!["order"]!["couponCodeSnapshot"]!.GetValue<string>());
        Assert.True(secondCheckoutJson["data"]!["order"]!["discountAmount"]!.GetValue<decimal>() > 0m);

        var voucherUsedCount = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Coupons
                .Where(coupon => coupon.Code == voucherCode)
                .Select(coupon => coupon.UsedCount)
                .SingleAsync());

        Assert.Equal(1, voucherUsedCount);
    }

    [Fact]
    public async Task LoyaltyEndpoints_ReturnAccountTransactionsTiersAndRedeemVoucher()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var tiersResponse = await client.GetAsync("/api/loyalty/tiers");
        var tiersJson = await tiersResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, tiersResponse.StatusCode);
        Assert.Equal(4, tiersJson["data"]!.AsArray().Count);

        using var unauthorizedResponse = await client.GetAsync("/api/loyalty/me");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        var token = await client.RegisterCustomerAsync();
        client.UseBearerToken(token);
        var customerId = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Customers
                .Where(customer => customer.Email == "customer@example.com")
                .Select(customer => customer.Id)
                .SingleAsync());

        using var emptyAccountResponse = await client.GetAsync("/api/loyalty/me");
        var emptyAccountJson = await emptyAccountResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, emptyAccountResponse.StatusCode);
        Assert.Equal(0, emptyAccountJson["data"]!["currentPoints"]!.GetValue<int>());
        Assert.Equal((int)LoyaltyTierType.Bronze, emptyAccountJson["data"]!["currentTier"]!.GetValue<int>());

        var catalog = TestData.CreateVisibleCatalog();
        var order = CreateCompletedOrder(customerId, catalog.Variant.Id);
        var account = new CustomerLoyaltyAccount(Guid.NewGuid(), customerId);
        account.EarnPoints(120, order.Id, "Earned from seeded order.");
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, order, account);

            return Task.CompletedTask;
        });

        using var accountResponse = await client.GetAsync("/api/loyalty/me");
        var accountJson = await accountResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, accountResponse.StatusCode);
        Assert.Equal(120, accountJson["data"]!["currentPoints"]!.GetValue<int>());
        Assert.Equal(120, accountJson["data"]!["totalPointsEarned"]!.GetValue<int>());

        using var transactionsResponse = await client.GetAsync("/api/loyalty/me/transactions?page=1&pageSize=20");
        var transactionsJson = await transactionsResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, transactionsResponse.StatusCode);
        Assert.Equal(1, transactionsJson["data"]!["totalCount"]!.GetValue<int>());
        var transaction = Assert.Single(transactionsJson["data"]!["items"]!.AsArray());
        Assert.Equal(120, transaction!["points"]!.GetValue<int>());
        Assert.Equal(order.Id, transaction["orderId"]!.GetValue<Guid>());

        using var redeemResponse = await client.PostAsJsonAsync(
            "/api/loyalty/me/redeem",
            new { points = 50 });
        var redeemJson = await redeemResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, redeemResponse.StatusCode);
        var voucherId = redeemJson["data"]!["voucherId"]!.GetValue<Guid>();
        var voucherCode = redeemJson["data"]!["voucherCode"]!.GetValue<string>();
        Assert.StartsWith(Coupon.LoyaltyVoucherCodePrefix, voucherCode, StringComparison.Ordinal);
        Assert.Equal(50000m, redeemJson["data"]!["discountAmount"]!.GetValue<decimal>());
        Assert.Equal(70, redeemJson["data"]!["remainingPoints"]!.GetValue<int>());

        var voucher = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Coupons
                .Where(coupon => coupon.Id == voucherId)
                .Select(coupon => new LoyaltyVoucherSnapshot(
                    coupon.CustomerId,
                    coupon.Source,
                    coupon.DiscountValue,
                    coupon.UsageLimit))
                .SingleAsync());

        Assert.Equal(customerId, voucher.CustomerId);
        Assert.Equal(CouponSource.Loyalty, voucher.Source);
        Assert.Equal(50000m, voucher.DiscountValue);
        Assert.Equal(1, voucher.UsageLimit);
    }

    private static Order CreateCompletedOrder(Guid customerId, Guid productVariantId)
    {
        var order = new Order(
            Guid.NewGuid(),
            "ORD-LOYALTY-001",
            customerId,
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "123 Shipping Street",
            "Call before delivery",
            PaymentMethod.Cod,
            "VND",
            1m);

        order.AddItem(
            Guid.NewGuid(),
            productVariantId,
            "Standing Desk",
            "DESK-001",
            1200000m,
            1,
            requiresInstallation: false);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, null, "admin@example.com");

        return order;
    }

    private sealed record LoyaltyVoucherSnapshot(
        Guid? CustomerId,
        CouponSource Source,
        decimal DiscountValue,
        int? UsageLimit);

    private static async Task AddCartItemAsync(HttpClient client, string sessionId, Guid productVariantId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/cart/items",
            new
            {
                sessionId,
                productVariantId,
                quantity = 1
            });

        response.EnsureSuccessStatusCode();
    }

    private static async Task UpdateOrderStatusAsync(HttpClient client, Guid orderId, int status)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new
            {
                status,
                note = "Loyalty manual flow verification"
            });

        response.EnsureSuccessStatusCode();
    }
}
