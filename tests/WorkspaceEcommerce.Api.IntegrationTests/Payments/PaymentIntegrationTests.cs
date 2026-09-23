using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

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
        var redirect = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("http://localhost:5173/checkout/payment-result", redirect.GetLeftPart(UriPartial.Path));
        Assert.Contains($"status=success&orderCode={seed.OrderCode}", redirect.Query, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(GetResultToken(redirect)));

        var queuedCommand = await fixture.ExecuteDbAsync(async dbContext =>
            await dbContext.ShipmentCommandOutbox
                .Where(command => command.OrderId == seed.OrderId)
                .Select(command => new
                {
                    command.CommandType,
                    command.Status
                })
                .SingleAsync());
        Assert.Equal(ShipmentCommandType.Create, queuedCommand.CommandType);
        Assert.Equal(ShipmentCommandStatus.Pending, queuedCommand.Status);
        Assert.Equal(1, await fixture.ProcessDueShipmentCommandsAsync());

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
        Assert.Equal($"ML-MOCK-{seed.OrderCode}", persisted.TrackingCode);
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
        var queuedAfterFirstCallback = await fixture.ExecuteDbAsync(async dbContext =>
            await dbContext.ShipmentCommandOutbox
                .Where(command => command.OrderId == seed.OrderId)
                .Select(command => new
                {
                    command.CommandType,
                    command.Status
                })
                .SingleAsync());
        Assert.Equal(ShipmentCommandType.Create, queuedAfterFirstCallback.CommandType);
        Assert.Equal(ShipmentCommandStatus.Pending, queuedAfterFirstCallback.Status);
        Assert.Equal(1, await fixture.ProcessDueShipmentCommandsAsync());

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

        var commandsAfterSecondCallback = await fixture.ExecuteDbAsync(async dbContext =>
            await dbContext.ShipmentCommandOutbox
                .Where(command => command.OrderId == seed.OrderId)
                .Select(command => new
                {
                    command.CommandType,
                    command.Status
                })
                .ToArrayAsync());
        var commandAfterSecondCallback = Assert.Single(commandsAfterSecondCallback);
        Assert.Equal(ShipmentCommandType.Create, commandAfterSecondCallback.CommandType);
        Assert.Equal(ShipmentCommandStatus.Completed, commandAfterSecondCallback.Status);
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
        var redirect = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("http://localhost:5173/checkout/payment-result", redirect.GetLeftPart(UriPartial.Path));
        Assert.Contains($"status=failed&orderCode={seed.OrderCode}", redirect.Query, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(GetResultToken(redirect)));

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

    [Theory]
    [InlineData("vnp_Amount", null)]
    [InlineData("vnp_Amount", "not-a-number")]
    [InlineData("vnp_Amount", "-100")]
    [InlineData("vnp_Amount", "99999999999999999999999999999999999999")]
    [InlineData("vnp_TransactionStatus", null)]
    public async Task VNPayIpn_WithSignedInvalidPayload_ReturnsInvalidRequestAndDoesNotMutatePayment(
        string field,
        string? value)
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-INVALID");
        var parameters = CreateVNPayCallbackParameters(
            seed.TxnRef,
            seed.Amount,
            "00",
            "00",
            "valid-hash");
        if (value is null)
        {
            parameters.Remove(field);
        }
        else
        {
            parameters[field] = value;
        }
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(
            $"/api/payments/vnpay/ipn{QueryString.Create(parameters)}");
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("99", json["RspCode"]!.GetValue<string>());
        var persisted = await fixture.ExecuteDbAsync(async dbContext => new
        {
            PaymentStatus = await dbContext.Orders
                .Where(order => order.Id == seed.OrderId)
                .Select(order => order.PaymentStatus)
                .SingleAsync(),
            TransactionStatus = await dbContext.PaymentTransactions
                .Where(transaction => transaction.TxnRef == seed.TxnRef)
                .Select(transaction => transaction.Status)
                .SingleAsync(),
            CommandCount = await dbContext.ShipmentCommandOutbox
                .CountAsync(command => command.OrderId == seed.OrderId)
        });
        Assert.Equal(PaymentStatus.Pending, persisted.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, persisted.TransactionStatus);
        Assert.Equal(0, persisted.CommandCount);
    }

    [Fact]
    public async Task VNPayReturn_WithSignedInvalidPayload_RedirectsWithoutOrderDataAndDoesNotMutatePayment()
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-INVALID-RETURN");
        var parameters = CreateVNPayCallbackParameters(
            seed.TxnRef,
            seed.Amount,
            "00",
            "00",
            "valid-hash");
        parameters.Remove("vnp_TransactionStatus");
        using var client = fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(
            $"/api/payments/vnpay/return{QueryString.Create(parameters)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var redirect = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("?status=failed", redirect.Query);
        Assert.Empty(redirect.Fragment);
        var persisted = await fixture.ExecuteDbAsync(async dbContext => new
        {
            PaymentStatus = await dbContext.Orders
                .Where(order => order.Id == seed.OrderId)
                .Select(order => order.PaymentStatus)
                .SingleAsync(),
            TransactionStatus = await dbContext.PaymentTransactions
                .Where(transaction => transaction.TxnRef == seed.TxnRef)
                .Select(transaction => transaction.Status)
                .SingleAsync()
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
    public async Task GetPaymentResult_RequiresAndAcceptsShortLivedPossessionProof()
    {
        await fixture.ResetDatabaseAsync();
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-0005");
        using var callbackClient = fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var callbackResponse = await callbackClient.GetAsync(CreateVNPayCallbackUrl(
            "/api/payments/vnpay/return",
            seed.TxnRef,
            seed.Amount,
            "00",
            "00",
            "valid-hash"));
        var redirect = Assert.IsType<Uri>(callbackResponse.Headers.Location);
        var resultToken = GetResultToken(redirect);
        Assert.False(string.IsNullOrWhiteSpace(resultToken));

        using var client = fixture.CreateClient();

        using var missingProofResponse = await client.GetAsync($"/api/payments/result?orderCode={seed.OrderCode}");
        using var invalidProofRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/payments/result?orderCode={seed.OrderCode}");
        invalidProofRequest.Headers.Add("X-Payment-Result-Token", "invalid-result-proof");
        using var invalidProofResponse = await client.SendAsync(invalidProofRequest);

        using var validProofRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/payments/result?orderCode={seed.OrderCode}");
        validProofRequest.Headers.Add("X-Payment-Result-Token", resultToken);
        using var response = await client.SendAsync(validProofRequest);
        var missingProofJson = await missingProofResponse.ReadJsonAsync();
        var invalidProofJson = await invalidProofResponse.ReadJsonAsync();
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, missingProofResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidProofResponse.StatusCode);
        Assert.Equal(
            missingProofJson["errors"]![0]!.GetValue<string>(),
            invalidProofJson["errors"]![0]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal(seed.OrderCode, json["data"]!["orderCode"]!.GetValue<string>());
        Assert.Equal((int)PaymentStatus.Paid, json["data"]!["paymentStatus"]!.GetValue<int>());
        Assert.Null(json["data"]!["orderId"]);
        Assert.Null(json["data"]!["shipmentId"]);
        Assert.Null(json["data"]!["transaction"]);
        Assert.Null(json["data"]!["gatewayResponseCode"]);
    }

    [Fact]
    public async Task GetPaymentResult_AuthenticatedCustomerCanReadOnlyOwnedOrder()
    {
        await fixture.ResetDatabaseAsync();
        using var ownerClient = fixture.CreateClient();
        var ownerToken = await ownerClient.RegisterCustomerAsync(
            "payment-owner@example.com",
            phoneNumber: "0900000001");
        var ownerId = await fixture.ExecuteDbAsync(async dbContext =>
            await dbContext.Customers
                .Where(customer => customer.Email == "payment-owner@example.com")
                .Select(customer => customer.Id)
                .SingleAsync());
        var seed = await SeedPendingVNPayPaymentAsync("ORD-PAY-OWNER", ownerId);
        ownerClient.UseBearerToken(ownerToken);

        using var attackerClient = fixture.CreateClient();
        attackerClient.UseBearerToken(await attackerClient.RegisterCustomerAsync(
            "payment-attacker@example.com",
            phoneNumber: "0900000002"));

        using var ownerResponse = await ownerClient.GetAsync($"/api/payments/result?orderCode={seed.OrderCode}");
        using var attackerResponse = await attackerClient.GetAsync($"/api/payments/result?orderCode={seed.OrderCode}");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, attackerResponse.StatusCode);
        var attackerJson = await attackerResponse.ReadJsonAsync();
        Assert.Equal("Payment result was not found.", attackerJson["errors"]![0]!.GetValue<string>());
    }

    private async Task<PaymentSeed> SeedPendingVNPayPaymentAsync(string orderCode, Guid? customerId = null)
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
                customerId,
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
        return $"{path}{QueryString.Create(CreateVNPayCallbackParameters(
            txnRef,
            amount,
            responseCode,
            transactionStatus,
            secureHash))}";
    }

    private static Dictionary<string, string?> CreateVNPayCallbackParameters(
        string txnRef,
        decimal amount,
        string responseCode,
        string transactionStatus,
        string secureHash)
    {
        var gatewayAmount = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)
            .ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        return new Dictionary<string, string?>
        {
            ["vnp_TmnCode"] = "TESTTMN",
            ["vnp_TxnRef"] = txnRef,
            ["vnp_Amount"] = gatewayAmount,
            ["vnp_BankCode"] = "NCB",
            ["vnp_OrderInfo"] = $"Pay order {txnRef}",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TransactionNo"] = "14123456",
            ["vnp_SecureHash"] = secureHash
        };
    }

    private static string? GetResultToken(Uri redirect)
    {
        var fragment = redirect.Fragment.TrimStart('#');
        return QueryString.FromUriComponent($"?{fragment}")
            .Value?
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2 && part[0] == "resultToken")
            .Select(part => Uri.UnescapeDataString(part[1]))
            .FirstOrDefault();
    }

    private sealed record PaymentSeed(
        Guid OrderId,
        string OrderCode,
        string TxnRef,
        decimal Amount);
}
