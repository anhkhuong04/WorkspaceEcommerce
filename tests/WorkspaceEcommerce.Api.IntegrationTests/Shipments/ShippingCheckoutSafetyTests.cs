using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

public sealed partial class ShippingCheckoutIntegrationTests
{
    [Fact]
    public async Task QuoteCannotBeAcceptedByAnotherAuthenticatedCustomerUsingSameSession()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier());
        using var owner = factory.CreateClient(); using var stranger = factory.CreateClient();
        owner.UseBearerToken(await owner.RegisterCustomerAsync("quote-owner@example.test"));
        var quote = await QuoteAsync(owner);
        stranger.UseBearerToken(await stranger.RegisterCustomerAsync("quote-stranger@example.test"));
        using var response = await stranger.PostAsJsonAsync("/api/checkout", Accept(quote));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync()));
    }

    [Fact]
    public async Task FailureAfterOrderSave_RollsBackQuoteInventoryCartAndAllWork()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier()).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IFulfillmentCoordinator>(); services.AddScoped<IFulfillmentCoordinator, FailingFulfillment>();
        }));
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client);
        using var response = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Set<OrderShippingSelection>().CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.OrderShipments.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(x => x.StockQuantity).SingleAsync()));
        Assert.Null(await fixture.ExecuteDbAsync(db => db.Set<ShippingQuote>().Select(x => x.OrderId).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Set<Domain.Modules.Cart.Cart>().CountAsync()));
    }

    [Fact]
    public async Task CancellationDuringExternalCreate_PersistsCompensatingCancelWithProviderMapping()
    {
        await SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var carrier = new CountingCarrier { Available = true, BeforeCreate = async () => { entered.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); } };
        await using var factory = Factory(carrier);
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { Method = ShippingMethod.MiniLogistics });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        using var scope = factory.Services.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<IOrderShipmentService>().ProcessDueCommandsAsync(10);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            client.UseBearerToken(await client.LoginAsAdminAsync());
            using var cancelled = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)OrderStatus.Cancelled, cancellationReason = "Cancelled during create" });
            Assert.True(cancelled.IsSuccessStatusCode);
        }
        finally { release.TrySetResult(); }
        await work;
        Assert.Equal(OrderStatus.Cancelled, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.Status).SingleAsync()));
        Assert.NotNull(await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.ShipmentId).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync(x => x.CommandType == ShipmentCommandType.Cancel && x.Status == ShipmentCommandStatus.Pending)));
    }
    private sealed class FailingFulfillment : IFulfillmentCoordinator
    {
        public Task<ShippingMethod?> MethodAsync(Guid orderId, CancellationToken cancellationToken) => Task.FromResult<ShippingMethod?>(ShippingMethod.WorkspaceEC);
        public Task AcceptAsync(Order order, ShippingMethod? method, CancellationToken cancellationToken) => throw new InvalidOperationException("Synthetic failure after order save");
        public Task CancelAsync(Order order, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task GuardStatusAsync(Order order, OrderStatus status, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<OrderStatus?> ProviderTargetAsync(Order order, OrderStatus? target, CancellationToken cancellationToken) => Task.FromResult(target);
    }
}
