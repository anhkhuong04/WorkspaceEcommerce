using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

public sealed partial class ShippingCheckoutIntegrationTests
{
    [Fact]
    public async Task NewOrderReadsExposeFrozenShippingAndSeparateIds_WithoutPrivateOperationalData()
    {
        await SeedAsync(); var carrier = new CountingCarrier(); await using var factory = Factory(carrier); using var client = factory.CreateClient();
        var quote = await QuoteAsync(client); using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        Assert.Equal(ShippingMethod.WorkspaceEC, order.Shipping!.Method); Assert.NotNull(order.Shipping.LocalShipmentId); Assert.Null(order.Shipping.ProviderShipmentId); Assert.Null(order.ShipmentId);
        client.UseBearerToken(await client.LoginAsAdminAsync());
        var local = await LocalAsync(client, order.Id);
        await ActAsync(client, order.Id, new(Guid.NewGuid(), local.Revision, WorkspaceShipmentAction.Assign, "PRIVATE-DELIVERY-NOTE", "PRIVATE-ASSIGNEE"));
        using var admin = await client.GetAsync($"/api/admin/orders/{order.Id}"); var adminDto = await DataAsync<AdminOrderDto>(admin);
        Assert.Equal(order.Shipping.CustomerCharge, adminDto.Shipping!.CustomerCharge);
        using var list = await client.GetAsync("/api/admin/orders?pageNumber=1&pageSize=20");
        var listJson = await list.ReadJsonAsync(); Assert.Equal(0, listJson["data"]!["items"]![0]!["shipping"]!["method"]!.GetValue<int>());
        client.DefaultRequestHeaders.Authorization = null;
        using var wrong = await client.GetAsync($"/api/orders/lookup?orderCode={order.OrderCode}&phone=wrong"); Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        using var lookup = await client.GetAsync($"/api/orders/lookup?orderCode={order.OrderCode}&phone={order.CustomerPhone}");
        var guest = await DataAsync<OrderLookupResponse>(lookup); Assert.Equal(order.Shipping.CustomerCharge, guest.Order.Shipping!.CustomerCharge);
        using var tracking = await client.GetAsync($"/api/orders/lookup/tracking?orderCode={order.OrderCode}&phone={order.CustomerPhone}");
        var raw = await tracking.Content.ReadAsStringAsync(); Assert.True(tracking.IsSuccessStatusCode);
        Assert.DoesNotContain("PRIVATE-DELIVERY-NOTE", raw); Assert.DoesNotContain("PRIVATE-ASSIGNEE", raw); Assert.DoesNotContain("actualCost", raw, StringComparison.OrdinalIgnoreCase);
        using var receipt = await client.GetAsync($"/api/orders/lookup/receipt?orderCode={order.OrderCode}&phone={order.CustomerPhone}");
        Assert.True(receipt.IsSuccessStatusCode); Assert.Equal("application/pdf", receipt.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString((await receipt.Content.ReadAsByteArrayAsync())[..8])); Assert.Equal(0, carrier.Calls);
    }
    [Fact]
    public async Task CustomerReadsRemainOwned_AndPagedShippingProjectionHasConstantQueryCount()
    {
        await SeedAsync(); await using var factory = Factory(new CountingCarrier()); using var owner = factory.CreateClient(); using var stranger = factory.CreateClient();
        owner.UseBearerToken(await owner.RegisterCustomerAsync("shipping-reader@example.test"));
        var quote = await QuoteAsync(owner); using var placed = await owner.PostAsJsonAsync("/api/checkout", Accept(quote)); var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        using var own = await owner.GetAsync($"/api/customer/orders/{order.Id}"); Assert.True(own.IsSuccessStatusCode);
        Assert.NotNull((await own.ReadJsonAsync())["data"]!["shipping"]);
        stranger.UseBearerToken(await stranger.RegisterCustomerAsync("shipping-stranger@example.test"));
        using var forbidden = await stranger.GetAsync($"/api/customer/orders/{order.Id}"); Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        using var tracking = await stranger.GetAsync($"/api/customer/orders/{order.Id}/tracking"); Assert.Equal(HttpStatusCode.NotFound, tracking.StatusCode);
        using var pdf = await stranger.GetAsync($"/api/customer/orders/{order.Id}/receipt"); Assert.Equal(HttpStatusCode.NotFound, pdf.StatusCode);
        var counter = factory.Services.GetRequiredService<SqlCommandCounter>();
        counter.Reset(); using var list = await owner.GetAsync("/api/customer/orders?pageNumber=1&pageSize=20"); Assert.True(list.IsSuccessStatusCode);
        Assert.Equal(3, counter.SelectCount); // Count/page plus one batched selection query.
    }
    [Fact]
    public async Task LocalQuoteSelectCountDoesNotGrowWithCartLines()
    {
        var seeded = await SeedAsync(); await using var factory = Factory(new CountingCarrier()); using var client = factory.CreateClient();
        var counter = factory.Services.GetRequiredService<SqlCommandCounter>();
        counter.Reset(); await QuoteAsync(client); var singleLine = counter.SelectCount;
        Assert.InRange(singleLine, 1, 10);
        await fixture.SeedAsync(async db =>
        {
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == seeded.VariantId);
            var cart = await db.Carts.Include(x => x.Items).SingleAsync();
            for (var index = 0; index < 19; index++)
            {
                var extra = new ProductVariant(Guid.NewGuid(), variant.ProductId, $"EXTRA-{index}", "Synthetic", null, null, 90000, null, 10, false, true, weightKg: 1);
                db.Add(extra); cart.AddItem(Guid.NewGuid(), extra.Id, 1, 90000);
            }
        });
        counter.Reset(); await QuoteAsync(client);
        Assert.Equal(singleLine, counter.SelectCount);
    }
    [Fact]
    public async Task ImportExplicitShippingSelectionIsRejectedAndLegacyPreviewIsLabelled()
    {
        await fixture.ResetDatabaseAsync(); using var client = fixture.CreateClient(); client.UseBearerToken(await client.LoginAsAdminAsync());
        using var file = new MultipartFormDataContent(); file.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("externalOrderCode,shippingMethod,sku,quantity\nIMPORT,WorkspaceEC,SYNTHETIC,1")), "file", "shipping.csv");
        using var response = await client.PostAsync("/api/admin/orders/import/preview", file); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var legacy = new MultipartFormDataContent();
        legacy.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("externalOrderCode,customerName,customerPhone,shippingAddress,paymentMethod,sku,quantity,shippingFee\nIMPORT,Synthetic Buyer,0900000000,Synthetic Street,Cod,SYNTHETIC,1,12345")), "file", "legacy.csv");
        using var preview = await client.PostAsync("/api/admin/orders/import/preview", legacy);
        Assert.True(preview.IsSuccessStatusCode);
        Assert.Equal("LegacyImport", (await preview.ReadJsonAsync())["data"]!["shippingSemantics"]!.GetValue<string>());
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync())); Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
    }
}
