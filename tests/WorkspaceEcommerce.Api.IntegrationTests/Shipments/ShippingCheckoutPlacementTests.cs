using System.Net;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using Cart = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

public sealed partial class ShippingCheckoutIntegrationTests
{
    private static CheckoutRequest Accept(ShippingQuoteDto quote, string session = "shipping-test", string street = "1 Synthetic Street", PaymentMethod payment = PaymentMethod.Cod) => new()
    {
        SessionId = session,
        CustomerName = "Synthetic Customer",
        CustomerPhone = "0900000000",
        ShippingStreet = street,
        ShippingProvinceCode = "01",
        ShippingWardCode = "00004",
        ShippingMethod = quote.Method,
        ShippingQuoteId = quote.Id,
        PaymentMethod = payment
    };

    [Fact]
    public async Task ConcurrentSameQuoteAndLostResponseRetry_CreateOneFrozenOrderAndReserveStockOnce()
    {
        var seed = await SeedAsync();
        var carrier = new CountingCarrier();
        await using var factory = Factory(carrier);
        using var first = factory.CreateClient(); using var second = factory.CreateClient();
        var quote = await QuoteAsync(first);
        var responses = await Task.WhenAll(first.PostAsJsonAsync("/api/checkout", Accept(quote)), second.PostAsJsonAsync("/api/checkout", Accept(quote)));
        var a = await DataAsync<CheckoutResponse>(responses[0]); var b = await DataAsync<CheckoutResponse>(responses[1]);
        Assert.Equal(a.Order.Id, b.Order.Id);
        Assert.Equal(105000, a.Order.TotalAmount); // 90k cart price, not current 100k catalog price.
        Assert.Equal(15000, a.Order.ShippingFee);
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(9, await fixture.ExecuteDbAsync(db => db.ProductVariants.Where(x => x.Id == seed.VariantId).Select(x => x.StockQuantity).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Set<OrderShippingSelection>().CountAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.OrderShipments.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        Assert.Equal(0, carrier.Calls);
        foreach (var response in responses) response.Dispose();
    }

    [Theory]
    [InlineData("session", 404)]
    [InlineData("street", 409)]
    [InlineData("payment", 409)]
    [InlineData("weight", 409)]
    [InlineData("expired", 409)]
    [InlineData("revoked", 409)]
    public async Task InvalidQuoteOrChangedInputs_DoNotPartiallyAcceptOrder(string change, int status)
    {
        var seed = await SeedAsync();
        await using var factory = Factory(new CountingCarrier());
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client);
        if (change == "weight") await fixture.ExecuteDbAsync(db => db.ProductVariants.ExecuteUpdateAsync(x => x.SetProperty(v => v.WeightKg, 2m)));
        if (change == "expired") await fixture.ExecuteDbAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE shipping.quotes SET created_at_utc = now() - interval '1 hour', expires_at_utc = now() - interval '1 minute' WHERE id = {quote.Id}"));
        if (change == "revoked") await fixture.SeedAsync(async db => (await db.Set<ShippingQuote>().SingleAsync()).Revoke(DateTimeOffset.UtcNow, "synthetic", "stop"));
        var request = Accept(quote, session: change == "session" ? "different-session" : "shipping-test", street: change == "street" ? "Changed street" : "1 Synthetic Street", payment: change == "payment" ? PaymentMethod.ManualBankTransfer : PaymentMethod.Cod);
        using var response = await client.PostAsJsonAsync("/api/checkout", request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.PaymentTransactions.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.OrderShipments.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Where(x => x.Id == seed.VariantId).Select(x => x.StockQuantity).SingleAsync()));
    }

    [Fact]
    public async Task TwoQuotesCompetingForLastStock_AcceptExactlyOne()
    {
        var seed = await SeedAsync(stock: 1);
        await fixture.SeedAsync(db => { var cart = new Cart(Guid.NewGuid(), null, "second-session"); cart.AddItem(Guid.NewGuid(), seed.VariantId, 1, 90000); db.Add(cart); return Task.CompletedTask; });
        await using var factory = Factory(new CountingCarrier());
        using var first = factory.CreateClient(); using var second = factory.CreateClient();
        var a = await QuoteAsync(first); var b = await QuoteAsync(second, Input("second-session"));
        var responses = await Task.WhenAll(first.PostAsJsonAsync("/api/checkout", Accept(a)), second.PostAsJsonAsync("/api/checkout", Accept(b, "second-session")));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => !x.IsSuccessStatusCode);
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(x => x.StockQuantity).SingleAsync()));
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task NewPublishedRatesHonorExistingQuote_AndConsumedQuoteRejectsDifferentDetails()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier());
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client);
        await fixture.SeedAsync(async db => { (await db.Set<ShippingRatePlan>().SingleAsync()).Retire(DateTimeOffset.UtcNow); });
        using var accepted = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        Assert.Equal(15000, (await DataAsync<CheckoutResponse>(accepted)).Order.ShippingFee);
        using var changed = await client.PostAsJsonAsync("/api/checkout", Accept(quote, street: "Another street"));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task VNPayUsesFrozenTotal_AndDuplicateCallbackCreatesNoExternalWorkForWorkspace()
    {
        await SeedAsync();
        var carrier = new CountingCarrier();
        await using var factory = Factory(carrier);
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { PaymentMethod = PaymentMethod.VNPay });
        using var response = await client.PostAsJsonAsync("/api/checkout", Accept(quote, payment: PaymentMethod.VNPay));
        var result = await DataAsync<CheckoutResponse>(response);
        var payment = await fixture.ExecuteDbAsync(db => db.PaymentTransactions.SingleAsync());
        Assert.Equal(result.Order.TotalAmount, payment.Amount);
        var callback = $"/api/payments/vnpay/ipn?vnp_TxnRef={payment.TxnRef}&vnp_Amount={payment.Amount * 100:0}&vnp_ResponseCode=00&vnp_TransactionStatus=00&vnp_TmnCode=TESTTMN&vnp_BankCode=NCB&vnp_OrderInfo=Synthetic&vnp_TransactionNo=12345&vnp_PayDate=20261029120000&vnp_SecureHash=valid-hash";
        using var paid = await client.GetAsync(callback); Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        using var repeat = await client.GetAsync(callback); Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(PaymentStatus.Paid, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.PaymentStatus).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.OrderShipments.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        Assert.Equal(0, carrier.Calls);
    }

    [Fact]
    public async Task WorkspaceRefreshRetryCancelAndOutboxReplay_NeverCallPartner()
    {
        await SeedAsync();
        var carrier = new CountingCarrier();
        await using var factory = Factory(carrier);
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client);
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var refresh = await client.PostAsync($"/api/admin/orders/{order.Id}/shipment/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        using var retry = await client.PostAsync($"/api/admin/orders/{order.Id}/shipment/retry", null);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        using var cancel = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)OrderStatus.Cancelled, cancellationReason = "Synthetic cancellation" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", await fixture.ExecuteDbAsync(db => db.OrderShipments.Select(x => x.ProviderStatus).SingleAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        await fixture.ExecuteScopeAsync(async services => { var db = services.GetRequiredService<WorkspaceEcommerce.Application.Abstractions.Persistence.IAppDbContext>(); await db.TryEnqueueShipmentCommandAsync(order.Id, ShipmentCommandType.Create, null, DateTimeOffset.UtcNow, CancellationToken.None); return true; });
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderShipmentService>().ProcessDueCommandsAsync(10);
        Assert.Equal(0, carrier.Calls);
        Assert.Equal(ShipmentCommandStatus.DeadLetter, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.Select(x => x.Status).SingleAsync()));
    }

    [Fact]
    public async Task ExternalBacklogContinuesWhenIntakeDisabled_AndCostOrCatalogChangesNeverRewriteCustomerCharge()
    {
        await SeedAsync();
        var carrier = new CountingCarrier { Available = true };
        await using var factory = Factory(carrier);
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { Method = ShippingMethod.MiniLogistics });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        carrier.Charge = 77000;
        await fixture.ExecuteDbAsync(db => db.ProductVariants.ExecuteUpdateAsync(x => x.SetProperty(v => v.WeightKg, 20m)));
        await using var disabledFactory = Factory(carrier, external: false);
        using (var scope = disabledFactory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderShipmentService>().ProcessDueCommandsAsync(10);
        Assert.Equal(2, carrier.Calls); // Quote on issuance + create; no worker requote.
        Assert.Equal(1.001m, carrier.LastCreate!.Parcel.WeightKg);
        Assert.Equal(order.TotalAmount, carrier.LastCreate.CodAmount);
        Assert.Equal(33000, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.ShippingFee).SingleAsync()));
        Assert.Equal(77000, await fixture.ExecuteDbAsync(db => db.OrderShipments.Select(x => x.ActualCostAmount).SingleAsync()));
    }
}
