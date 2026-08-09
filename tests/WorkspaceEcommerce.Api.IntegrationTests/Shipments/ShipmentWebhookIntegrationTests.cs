using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class ShipmentWebhookIntegrationTests(ApiIntegrationTestFixture fixture) : IAsyncLifetime
{
    private const string Secret = "integration-webhook-secret";

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Webhook_ValidSignature_UpdatesLocalShipmentAndOrder()
    {
        var order = CreateOrder();
        var shipment = CreateShipment(order);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Orders.Add(order);
            dbContext.OrderShipments.Add(shipment);
            return Task.CompletedTask;
        });
        var payload = new ShipmentWebhookPayload(
            Guid.NewGuid(),
            ShipmentProviderContract.ShipmentStatusChangedEvent,
            shipment.TrackingCode,
            order.OrderCode,
            "InTransit",
            DateTimeOffset.UtcNow);

        using var client = fixture.CreateClient();
        using var response = await SendWebhookAsync(client, payload, DateTimeOffset.UtcNow, validSignature: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await fixture.ExecuteDbAsync(dbContext => Task.FromResult(new
        {
            OrderStatus = dbContext.Orders.Single(candidate => candidate.Id == order.Id).Status,
            ShipmentStatus = dbContext.OrderShipments.Single(candidate => candidate.OrderId == order.Id).ProviderStatus,
            TimelineCount = dbContext.ShipmentTimelineEntries.Count(entry => entry.OrderShipmentId == shipment.Id),
            InboxCount = dbContext.ShipmentEventInbox.Count()
        }));
        Assert.Equal(OrderStatus.Shipping, state.OrderStatus);
        Assert.Equal("InTransit", state.ShipmentStatus);
        Assert.Equal(1, state.TimelineCount);
        Assert.Equal(1, state.InboxCount);
    }

    [Fact]
    public async Task Webhook_DuplicateEvent_IsIdempotent()
    {
        var order = CreateOrder();
        var shipment = CreateShipment(order);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Orders.Add(order);
            dbContext.OrderShipments.Add(shipment);
            return Task.CompletedTask;
        });
        var payload = new ShipmentWebhookPayload(
            Guid.NewGuid(),
            ShipmentProviderContract.ShipmentStatusChangedEvent,
            shipment.TrackingCode,
            order.OrderCode,
            "InTransit",
            DateTimeOffset.UtcNow);
        using var client = fixture.CreateClient();

        using var firstResponse = await SendWebhookAsync(client, payload, DateTimeOffset.UtcNow, validSignature: true);
        using var secondResponse = await SendWebhookAsync(client, payload, DateTimeOffset.UtcNow, validSignature: true);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var state = await fixture.ExecuteDbAsync(dbContext => Task.FromResult(new
        {
            TimelineCount = dbContext.ShipmentTimelineEntries.Count(entry => entry.OrderShipmentId == shipment.Id),
            InboxCount = dbContext.ShipmentEventInbox.Count()
        }));
        Assert.Equal(1, state.TimelineCount);
        Assert.Equal(1, state.InboxCount);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient();
        using var response = await SendWebhookAsync(
            client,
            CreatePayload(),
            DateTimeOffset.UtcNow,
            validSignature: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(10)]
    public async Task Webhook_TimestampOutsideTolerance_ReturnsUnauthorized(int offsetMinutes)
    {
        using var client = fixture.CreateClient();
        using var response = await SendWebhookAsync(
            client,
            CreatePayload(),
            DateTimeOffset.UtcNow.AddMinutes(offsetMinutes),
            validSignature: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendWebhookAsync(
        HttpClient client,
        ShipmentWebhookPayload payload,
        DateTimeOffset timestamp,
        bool validSignature)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var timestampText = timestamp.ToString("O");
        var signature = ComputeSignature(validSignature ? Secret : "wrong-secret", timestampText, json);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/minilogistics")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-MiniLogistics-Event", payload.Event);
        request.Headers.Add("X-MiniLogistics-Timestamp", timestampText);
        request.Headers.Add("X-MiniLogistics-Signature", signature);
        return await client.SendAsync(request);
    }

    private static string ComputeSignature(string secret, string timestamp, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static ShipmentWebhookPayload CreatePayload()
    {
        return new ShipmentWebhookPayload(
            Guid.NewGuid(),
            ShipmentProviderContract.ShipmentStatusChangedEvent,
            "ML-NOT-USED",
            "ORD-NOT-USED",
            "InTransit",
            DateTimeOffset.UtcNow);
    }

    private static Order CreateOrder()
    {
        var order = new Order(
            Guid.NewGuid(),
            "ORD-20260802-INTEGRATION",
            null,
            "Integration Customer",
            "0900000000",
            null,
            "1 Integration Street",
            null,
            PaymentMethod.Cod,
            "VND",
            1m);
        order.UpdateShipmentInfo("ML-INTEGRATION-1", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        return order;
    }

    private static OrderShipment CreateShipment(Order order)
    {
        return new OrderShipment(
            Guid.NewGuid(),
            order.Id,
            ShipmentProviderContract.ProviderName,
            order.ShipmentId!.Value,
            order.TrackingCode!,
            "PendingPickup",
            30000m,
            "VND",
            DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
