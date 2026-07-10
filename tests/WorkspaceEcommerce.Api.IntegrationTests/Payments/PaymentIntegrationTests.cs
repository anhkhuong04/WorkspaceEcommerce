using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Api.IntegrationTests.Payments;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class PaymentIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task VNPayReturn_WithSuccessCallback_MarksPaymentPaidCreatesShipmentAndRedirects()
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-0001");
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(CreateVNPayCallbackUrl(
            "/api/payments/vnpay/return",
            seed.TxnRef,
            seed.Amount,
            "00",
            "00",
            "valid-hash"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            $"http://localhost:5173/checkout/payment-result?status=success&orderCode={seed.OrderCode}",
            response.Headers.Location?.ToString());

        var persisted = await fixture.ExecuteDbAsync(async dbContext =>
        {
            var order = await dbContext.Orders.SingleAsync(existing => existing.Id == seed.OrderId);
            var transaction = await dbContext.PaymentTransactions.SingleAsync(existing => existing.TxnRef == seed.TxnRef);

            return new
            {
                order.PaymentStatus,
                order.PaidAt,
                order.ShipmentId,
                order.TrackingCode,
                TransactionStatus = transaction.Status,
                transaction.GatewayResponseCode
            };
        });

        Assert.Equal(PaymentStatus.Paid, persisted.PaymentStatus);
        Assert.NotNull(persisted.PaidAt);
        Assert.NotNull(persisted.ShipmentId);
        Assert.Equal("ML-MOCK-INT", persisted.TrackingCode);
        Assert.Equal(PaymentTransactionStatus.Success, persisted.TransactionStatus);
        Assert.Equal("00", persisted.GatewayResponseCode);
    }

    [Fact]
    public async Task VNPayIpn_DuplicateSuccessCallback_ReturnsSuccessAndDoesNotChangeShipment()
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-0002");
        using var client = fixture.CreateClient();
        var callbackUrl = CreateVNPayCallbackUrl(
            "/api/payments/vnpay/ipn",
            seed.TxnRef,
            seed.Amount,
            "00",
            "00",
            "valid-hash");

        using var firstResponse = await client.GetAsync(callbackUrl);
        var shipmentAfterFirst = await fixture.ExecuteDbAsync(async dbContext =>
            await dbContext.Orders
                .Where(order => order.Id == seed.OrderId)
                .Select(order => order.ShipmentId)
                .SingleAsync());
        using var secondResponse = await client.GetAsync(callbackUrl);
        var shipmentAfterSecond = await fixture.ExecuteDbAsync(async dbContext =>
            await dbContext.Orders
                .Where(order => order.Id == seed.OrderId)
                .Select(order => order.ShipmentId)
                .SingleAsync());

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal("00", (await firstResponse.ReadJsonAsync())["RspCode"]!.GetValue<string>());
        Assert.Equal("00", (await secondResponse.ReadJsonAsync())["RspCode"]!.GetValue<string>());
        Assert.NotNull(shipmentAfterFirst);
        Assert.Equal(shipmentAfterFirst, shipmentAfterSecond);
    }

    [Fact]
    public async Task VNPayReturn_WithFailedCallback_MarksPaymentFailedAndRedirects()
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-0003");
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(CreateVNPayCallbackUrl(
            "/api/payments/vnpay/return",
            seed.TxnRef,
            seed.Amount,
            "99",
            "02",
            "valid-hash"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            $"http://localhost:5173/checkout/payment-result?status=failed&orderCode={seed.OrderCode}",
            response.Headers.Location?.ToString());

        var persisted = await fixture.ExecuteDbAsync(async dbContext =>
        {
            var order = await dbContext.Orders.SingleAsync(existing => existing.Id == seed.OrderId);
            var transaction = await dbContext.PaymentTransactions.SingleAsync(existing => existing.TxnRef == seed.TxnRef);

            return new
            {
                order.PaymentStatus,
                order.ShipmentId,
                TransactionStatus = transaction.Status
            };
        });

        Assert.Equal(PaymentStatus.Failed, persisted.PaymentStatus);
        Assert.Null(persisted.ShipmentId);
        Assert.Equal(PaymentTransactionStatus.Failed, persisted.TransactionStatus);
    }

    [Fact]
    public async Task VNPayIpn_WithTamperedHash_ReturnsChecksumFailureAndDoesNotMutatePayment()
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-0004");
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(CreateVNPayCallbackUrl(
            "/api/payments/vnpay/ipn",
            seed.TxnRef,
            seed.Amount,
            "00",
            "00",
            "tampered-hash"));
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("97", json["RspCode"]!.GetValue<string>());

        var persisted = await fixture.ExecuteDbAsync(async dbContext =>
        {
            var order = await dbContext.Orders.SingleAsync(existing => existing.Id == seed.OrderId);
            var transaction = await dbContext.PaymentTransactions.SingleAsync(existing => existing.TxnRef == seed.TxnRef);

            return new
            {
                order.PaymentStatus,
                TransactionStatus = transaction.Status
            };
        });

        Assert.Equal(PaymentStatus.Pending, persisted.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, persisted.TransactionStatus);
    }

    [Fact]
    public async Task VNPayIpn_WithUnknownTxnRef_ReturnsOrderNotFound()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(CreateVNPayCallbackUrl(
            "/api/payments/vnpay/ipn",
            "MISSING-TXN",
            100_000m,
            "00",
            "00",
            "valid-hash"));
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("01", json["RspCode"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetPaymentResult_ReturnsPaymentEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-0005");
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync($"/api/payments/result?orderCode={seed.OrderCode}&phone=0900000000");
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal(seed.OrderCode, json["data"]!["orderCode"]!.GetValue<string>());
        Assert.Equal((int)PaymentStatus.Pending, json["data"]!["paymentStatus"]!.GetValue<int>());
        Assert.Equal(seed.TxnRef, json["data"]!["transaction"]!["txnRef"]!.GetValue<string>());
    }

    private async Task<PaymentSeed> SeedPendingVNPayPaymentAsync(string orderCode)
    {
        var orderId = Guid.NewGuid();
        var catalog = TestData.CreateVisibleCatalog();
        var txnRef = $"{orderCode}-TXN";
        const decimal amount = 100_000m;

        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);

            var order = new Order(
                orderId,
                orderCode,
                null,
                "Nguyen Van A",
                "0900000000",
                "customer@example.com",
                "123 Shipping Street, Ward 1, Ho Chi Minh",
                "Call before delivery",
                PaymentMethod.VNPay,
                "VND",
                1m);
            order.SetShippingAddressDetails(
                "123 Shipping Street",
                "Ward 1",
                "Ho Chi Minh");
            order.AddItem(
                Guid.NewGuid(),
                catalog.Variant.Id,
                "Standing Desk",
                "DESK-001",
                amount,
                1,
                requiresInstallation: false);

            var transaction = new PaymentTransaction(
                Guid.NewGuid(),
                order.Id,
                PaymentProvider.VNPay,
                order.TotalAmount,
                order.CurrencyCode,
                txnRef);

            dbContext.Add(order);
            dbContext.Add(transaction);

            return Task.CompletedTask;
        });

        return new PaymentSeed(orderId, orderCode, txnRef, amount);
    }

    private static string CreateVNPayCallbackUrl(
        string path,
        string txnRef,
        decimal amount,
        string responseCode,
        string transactionStatus,
        string secureHash)
    {
        var gatewayAmount = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)
            .ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        var query = new Dictionary<string, string?>
        {
            ["vnp_TxnRef"] = txnRef,
            ["vnp_Amount"] = gatewayAmount,
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TransactionNo"] = "14123456",
            ["vnp_SecureHash"] = secureHash
        };

        return $"{path}{QueryString.Create(query)}";
    }

    private sealed record PaymentSeed(
        Guid OrderId,
        string OrderCode,
        string TxnRef,
        decimal Amount);
}
