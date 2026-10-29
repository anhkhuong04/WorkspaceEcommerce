using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using Cart = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed partial class ShippingCheckoutIntegrationTests(ApiIntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static ShippingQuoteInput Input(string session = "shipping-test") => new()
    {
        SessionId = session,
        ShippingStreet = "1 Synthetic Street",
        ProvinceCode = "01",
        WardCode = "00004",
        Method = ShippingMethod.WorkspaceEC,
        PaymentMethod = Domain.Modules.Ordering.PaymentMethod.Cod
    };
    private WebApplicationFactory<Program> Factory(CountingCarrier carrier, bool external = true) => fixture.CreateFactory(new Dictionary<string, string?>
    {
        ["ShippingCheckout:Enabled"] = "true",
        ["ShippingCheckout:ExternalEnabled"] = external.ToString(),
    }).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<IShipmentService>(); services.AddSingleton<IShipmentService>(carrier);
        foreach (var worker in services.Where(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
            && x.ImplementationType?.Name == "ShipmentCommandWorker").ToArray()) services.Remove(worker);
    }));

    [Fact]
    public async Task OptionsAndWorkspaceQuote_DoNotWaitForOrCallUnavailableProvider()
    {
        await SeedAsync();
        var carrier = new CountingCarrier();
        await using var factory = Factory(carrier);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/checkout/shipping/options", Input());
        var options = await DataAsync<ShippingMethodOption[]>(response);
        Assert.True(options.Single(x => x.Method == ShippingMethod.WorkspaceEC).Available);
        var quote = await QuoteAsync(client);
        Assert.Equal(15000, quote.CustomerCharge);
        Assert.Equal(1001, quote.WeightGrams);
        Assert.Equal("VND", quote.Currency);
        Assert.Equal(0, carrier.Calls);
        using var unavailable = await client.PostAsJsonAsync("/api/checkout/shipping/quotes", Input() with { Method = ShippingMethod.MiniLogistics });
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        Assert.Equal(1, carrier.Calls);
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Set<ShippingQuote>().CountAsync()));
    }
    [Fact]
    public async Task DisabledExternalIntakeAndInvalidGeography_AreExplicitAndDoNotCreateFreeQuotes()
    {
        await SeedAsync();
        var carrier = new CountingCarrier();
        await using var factory = Factory(carrier, external: false);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/checkout/shipping/options", Input());
        Assert.False((await DataAsync<ShippingMethodOption[]>(response)).Single(x => x.Method == ShippingMethod.MiniLogistics).Available);
        using var disabled = await client.PostAsJsonAsync("/api/checkout/shipping/quotes", Input() with { Method = ShippingMethod.MiniLogistics });
        Assert.Equal(HttpStatusCode.Conflict, disabled.StatusCode);
        using var invalid = await client.PostAsJsonAsync("/api/checkout/shipping/quotes", Input() with { WardCode = "99999" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, carrier.Calls);
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Set<ShippingQuote>().CountAsync()));
    }
    [Fact]
    public async Task QuoteRevocation_RequiresAdminAndPersistsServerActorAndReason()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier());
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client);
        using var denied = await client.PostAsJsonAsync($"/api/admin/shipping/quotes/{quote.Id}/revoke", new RevokeShippingQuoteRequest("Emergency"));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var revoked = await client.PostAsJsonAsync($"/api/admin/shipping/quotes/{quote.Id}/revoke", new RevokeShippingQuoteRequest("Emergency stop"));
        Assert.True(revoked.IsSuccessStatusCode);
        var persisted = await fixture.ExecuteDbAsync(db => db.Set<ShippingQuote>().SingleAsync());
        Assert.NotNull(persisted.RevokedAtUtc);
        Assert.StartsWith("admin-session:", persisted.RevocationActor);
        Assert.Equal("Emergency stop", persisted.RevocationReason);
    }
    private async Task<(Guid VariantId, Guid PlanId)> SeedAsync(string session = "shipping-test", int stock = 10)
    {
        await fixture.ResetDatabaseAsync();
        var catalog = await fixture.ExecuteScopeAsync(services => Task.FromResult(services.GetRequiredService<IShippingAdministrativeCatalog>()));
        var category = new Category(Guid.NewGuid(), null, LocalizedText.Of("Shipping test"), "shipping-category", 1, true);
        var product = new Product(Guid.NewGuid(), category.Id, LocalizedText.Of("Shipping product"), "shipping-product", null, true);
        var variant = new ProductVariant(Guid.NewGuid(), product.Id, "SHIPPING-SKU", "Weighted", null, null, 100000, null, stock, false, true, weightKg: 1.001m);
        var cart = new Cart(Guid.NewGuid(), null, session);
        cart.AddItem(Guid.NewGuid(), variant.Id, 1, 90000); // Deliberately preserve cart price snapshot.
        var now = DateTimeOffset.UtcNow;
        var plan = new ShippingRatePlan(Guid.NewGuid(), "Synthetic only", catalog.Version, "01", now);
        plan.ReplaceDraft(plan.Name, "01", [new ShippingRateZone(Guid.NewGuid(), plan.Id, "ALL", "All", 1000, 10000, 500, 5000, null, catalog.Provinces.Select(x => x.Code).ToArray())], now);
        plan.Publish(now);
        await fixture.SeedAsync(db => { db.AddRange(category, product, variant, cart, plan); return Task.CompletedTask; });
        return (variant.Id, plan.Id);
    }
    private static async Task<ShippingQuoteDto> QuoteAsync(HttpClient client, ShippingQuoteInput? input = null)
    {
        using var response = await client.PostAsJsonAsync("/api/checkout/shipping/quotes", input ?? Input());
        return await DataAsync<ShippingQuoteDto>(response);
    }
    private static async Task<T> DataAsync<T>(HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {json}");
        return JsonSerializer.Deserialize<T>(json["data"]!.ToJsonString(), JsonOptions)!;
    }
    private sealed class CountingCarrier : IShipmentService
    {
        public int Calls;
        public bool Available { get; set; }
        public decimal Charge = 33000;
        public CreateShipmentRequest? LastCreate;
        public Func<Task>? BeforeCreate;
        public Task<ShippingQuoteResponse> GetShippingQuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return Available ? Task.FromResult(new ShippingQuoteResponse { TotalFeeAmount = Charge, Currency = "VND" }) : Task.FromException<ShippingQuoteResponse>(new HttpRequestException("Synthetic outage"));
        }
        public async Task<CreateShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            if (!Available) return await Fail<CreateShipmentResponse>();
            Interlocked.Increment(ref Calls); LastCreate = request;
            if (BeforeCreate is not null) await BeforeCreate();
            return new CreateShipmentResponse { ShipmentId = Guid.NewGuid(), ExternalOrderId = request.ExternalOrderId, TrackingCode = "SYNTHETIC-TRACKING", Status = "Draft", ShippingFeeAmount = Charge, Currency = "VND" };
        }
        public Task<TrackingResponse> GetTrackingAsync(string trackingCode, CancellationToken cancellationToken = default) => Fail<TrackingResponse>();
        public Task<TrackingResponse> CancelShipmentAsync(string trackingCode, string reason, CancellationToken cancellationToken = default) => Fail<TrackingResponse>();
        private Task<T> Fail<T>() { Interlocked.Increment(ref Calls); return Task.FromException<T>(new HttpRequestException("Synthetic outage")); }
    }
}
