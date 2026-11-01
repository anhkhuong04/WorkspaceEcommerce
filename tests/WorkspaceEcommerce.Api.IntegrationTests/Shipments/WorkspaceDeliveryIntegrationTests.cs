using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Payments;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

public sealed partial class ShippingCheckoutIntegrationTests
{
    [Fact]
    public async Task WorkspaceLifecycle_FailureRetryDeliveryAndCodCollection_AreAtomicAndIdempotent()
    {
        await SeedAsync(); var carrier = new CountingCarrier();
        await using var factory = Factory(carrier); using var client = factory.CreateClient();
        var quote = await QuoteAsync(client);
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        client.UseBearerToken(await client.LoginAsAdminAsync());
        var state = await LocalAsync(client, order.Id);
        var assign = new WorkspaceShipmentRequest(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Assign, "Private dispatch note", "OPERATIONS-001");
        state = await ActAsync(client, order.Id, assign);
        Assert.Equal(state.Revision, (await ActAsync(client, order.Id, assign)).Revision);
        using var premature = await client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/workspace-shipment/actions", new WorkspaceShipmentRequest(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Dispatch, "Premature"));
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        using var confirmed = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = 1 }); Assert.True(confirmed.IsSuccessStatusCode);
        state = await ActAsync(client, order.Id, new(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Dispatch, "Dispatch"));
        Assert.Equal(OrderStatus.Shipping, state.OrderStatus);
        state = await ActAsync(client, order.Id, new(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.FailDelivery, "Recipient unavailable"));
        Assert.Equal(OrderStatus.FailedDelivery, state.OrderStatus);
        state = await ActAsync(client, order.Id, new(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.RetryDelivery, "Confirmed retry"));
        var delivery = new WorkspaceShipmentRequest(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Deliver, "Delivery observed");
        state = await ActAsync(client, order.Id, delivery);
        Assert.Equal("Delivered", state.Status); Assert.Equal(OrderStatus.Shipping, state.OrderStatus); Assert.Equal(PaymentStatus.Unpaid, state.PaymentStatus);
        using var collected = await client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/payment/collect-cod", new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "LOCAL-COD", "Cash counted"));
        Assert.True(collected.IsSuccessStatusCode);
        state = await ActAsync(client, order.Id, delivery);
        Assert.Equal(OrderStatus.Completed, state.OrderStatus);
        Assert.Equal(5, await fixture.ExecuteDbAsync(db => db.Set<WorkspaceShipmentOperation>().CountAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.OrderStatusHistories.CountAsync(x => x.ToStatus == OrderStatus.Completed)));
        Assert.NotNull(await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.CompletedAt).SingleAsync()));
        Assert.Equal(0, carrier.Calls);
    }
    [Fact]
    public async Task WorkspaceTransfer_UnpaidCannotDispatch_AndPaidDeliveryCompletes()
    {
        await SeedAsync(); await using var factory = Factory(new CountingCarrier()); using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { PaymentMethod = PaymentMethod.ManualBankTransfer });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote, payment: PaymentMethod.ManualBankTransfer));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order; client.UseBearerToken(await client.LoginAsAdminAsync());
        var state = await LocalAsync(client, order.Id);
        state = await ActAsync(client, order.Id, new(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Assign, "Assign", "OPERATIONS-002"));
        using var confirmed = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = 1 }); Assert.True(confirmed.IsSuccessStatusCode);
        using var denied = await client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/workspace-shipment/actions", new WorkspaceShipmentRequest(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Dispatch, "Unpaid")); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using var paid = await client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/payment/confirm-transfer", new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "LOCAL-BANK", "Verified")); Assert.True(paid.IsSuccessStatusCode);
        state = await ActAsync(client, order.Id, new(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Dispatch, "Paid dispatch"));
        state = await ActAsync(client, order.Id, new(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Deliver, "Paid delivery"));
        Assert.Equal(OrderStatus.Completed, state.OrderStatus);
    }
    [Fact]
    public async Task WorkspaceCancelAndDispatchRace_HasOneConsistentOutcomeAndNoPartnerWork()
    {
        await SeedAsync(); await using var factory = Factory(new CountingCarrier()); using var first = factory.CreateClient(); using var second = factory.CreateClient();
        var quote = await QuoteAsync(first); using var placed = await first.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order; var token = await first.LoginAsAdminAsync(); first.UseBearerToken(token); second.UseBearerToken(token);
        var state = await LocalAsync(first, order.Id);
        state = await ActAsync(first, order.Id, new(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Assign, "Assign", "OPERATIONS-003"));
        using var confirmed = await first.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = 1 }); Assert.True(confirmed.IsSuccessStatusCode);
        var responses = await Task.WhenAll(first.PostAsJsonAsync($"/api/admin/orders/{order.Id}/workspace-shipment/actions", new WorkspaceShipmentRequest(Guid.NewGuid(), state.Revision, WorkspaceShipmentAction.Dispatch, "Dispatch race")), second.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = 6, cancellationReason = "Cancel race", expectedShipmentRevision = state.Revision }));
        Assert.Single(responses, x => x.IsSuccessStatusCode); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = await LocalAsync(first, order.Id);
        Assert.True(current.OrderStatus == OrderStatus.Shipping && current.Status == "OutForDelivery" || current.OrderStatus == OrderStatus.Cancelled && current.Status == "Cancelled");
        Assert.Equal(current.OrderStatus == OrderStatus.Cancelled ? 10 : 9, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(x => x.StockQuantity).SingleAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        foreach (var response in responses) response.Dispose();
    }
    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "/actions")]
    [InlineData("GET", "/history")]
    public async Task WorkspaceEndpoints_AreAdminOnly(string method, string suffix)
    {
        await fixture.ResetDatabaseAsync(); using var client = fixture.CreateClient();
        var path = $"/api/admin/orders/{Guid.NewGuid()}/workspace-shipment{suffix}";
        using var anonymous = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = method == "POST" ? JsonContent.Create(new { }) : null }); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.UseBearerToken(await client.RegisterCustomerAsync());
        using var customer = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = method == "POST" ? JsonContent.Create(new { }) : null }); Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
    }
    private static async Task<WorkspaceShipmentDto> LocalAsync(HttpClient client, Guid orderId)
    { using var response = await client.GetAsync($"/api/admin/orders/{orderId}/workspace-shipment"); return await DataAsync<WorkspaceShipmentDto>(response); }
    private static async Task<WorkspaceShipmentDto> ActAsync(HttpClient client, Guid orderId, WorkspaceShipmentRequest request)
    { using var response = await client.PostAsJsonAsync($"/api/admin/orders/{orderId}/workspace-shipment/actions", request); return await DataAsync<WorkspaceShipmentDto>(response); }
}
