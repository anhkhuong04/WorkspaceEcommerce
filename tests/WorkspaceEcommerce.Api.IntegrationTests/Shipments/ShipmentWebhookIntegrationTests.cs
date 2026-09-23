using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
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
    public async Task Webhook_ConcurrentDuplicateDeliveredEvent_AppliesDurableSideEffectsOnce()
    {
        var catalog = TestData.CreateVisibleCatalog();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);
            return Task.CompletedTask;
        });
        using var registrationClient = fixture.CreateClient();
        await registrationClient.RegisterCustomerAsync(
            email: $"shipment-{Guid.NewGuid():N}@example.com",
            password: "customer-password");
        var customerId = await fixture.ExecuteDbAsync(dbContext =>
            Task.FromResult(dbContext.Customers.Single().Id));
        var order = CreateOrder(customerId);
        order.AddItem(
            Guid.NewGuid(),
            catalog.Variant.Id,
            "Standing Desk",
            catalog.Variant.Sku,
            100_000m,
            1,
            requiresInstallation: false);
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
            "Delivered",
            DateTimeOffset.UtcNow);
        using var firstClient = fixture.CreateClient();
        using var secondClient = fixture.CreateClient();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequest = SendAfterAsync(firstClient, payload, start.Task);
        var secondRequest = SendAfterAsync(secondClient, payload, start.Task);

        start.SetResult();
        var responses = await Task.WhenAll(firstRequest, secondRequest);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var state = await fixture.ExecuteDbAsync(dbContext => Task.FromResult(new
        {
            OrderStatus = dbContext.Orders.Single(candidate => candidate.Id == order.Id).Status,
            ShipmentStatus = dbContext.OrderShipments.Single(candidate => candidate.OrderId == order.Id).ProviderStatus,
            TimelineCount = dbContext.ShipmentTimelineEntries.Count(entry => entry.OrderShipmentId == shipment.Id),
            InboxCount = dbContext.ShipmentEventInbox.Count(entry => entry.Id == payload.EventId),
            EarnCount = dbContext.LoyaltyTransactions.Count(transaction =>
                transaction.OrderId == order.Id && transaction.Type == LoyaltyTransactionType.Earn)
        }));
        Assert.Equal(OrderStatus.Completed, state.OrderStatus);
        Assert.Equal("Delivered", state.ShipmentStatus);
        Assert.Equal(1, state.TimelineCount);
        Assert.Equal(1, state.InboxCount);
        Assert.Equal(1, state.EarnCount);
    }

    [Fact]
    public async Task Webhook_OlderEventCommitsAfterNewerEvent_DoesNotRegressShipmentOrOrder()
    {
        var order = CreateOrder();
        var shipment = CreateShipment(order);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Orders.Add(order);
            dbContext.OrderShipments.Add(shipment);
            return Task.CompletedTask;
        });
        var changedAt = DateTimeOffset.UtcNow;
        var newerPayload = new ShipmentWebhookPayload(
            Guid.NewGuid(),
            ShipmentProviderContract.ShipmentStatusChangedEvent,
            shipment.TrackingCode,
            order.OrderCode,
            "InTransit",
            changedAt.AddMinutes(1));
        var olderPayload = new ShipmentWebhookPayload(
            Guid.NewGuid(),
            ShipmentProviderContract.ShipmentStatusChangedEvent,
            shipment.TrackingCode,
            order.OrderCode,
            "PendingPickup",
            changedAt);
        using var newerClient = fixture.CreateClient();
        using var olderClient = fixture.CreateClient();

        using var newerResponse = await SendWebhookAsync(
            newerClient,
            newerPayload,
            DateTimeOffset.UtcNow,
            validSignature: true);
        using var olderResponse = await SendWebhookAsync(
            olderClient,
            olderPayload,
            DateTimeOffset.UtcNow,
            validSignature: true);

        Assert.Equal(HttpStatusCode.OK, newerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, olderResponse.StatusCode);
        var state = await fixture.ExecuteDbAsync(dbContext => Task.FromResult(new
        {
            OrderStatus = dbContext.Orders.Single(candidate => candidate.Id == order.Id).Status,
            Shipment = dbContext.OrderShipments.Single(candidate => candidate.OrderId == order.Id),
            InboxCount = dbContext.ShipmentEventInbox.Count()
        }));
        Assert.Equal(OrderStatus.Shipping, state.OrderStatus);
        Assert.Equal("InTransit", state.Shipment.ProviderStatus);
        Assert.Equal(newerPayload.ChangedAtUtc.ToUnixTimeMilliseconds(), state.Shipment.LastEventAtUtc.ToUnixTimeMilliseconds());
        Assert.Equal(2, state.InboxCount);
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

    private static async Task<HttpResponseMessage> SendAfterAsync(
        HttpClient client,
        ShipmentWebhookPayload payload,
        Task start)
    {
        await start;
        return await SendWebhookAsync(client, payload, DateTimeOffset.UtcNow, validSignature: true);
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

    private static Order CreateOrder(Guid? customerId = null)
    {
        var order = new Order(
            Guid.NewGuid(),
            "ORD-20260802-INTEGRATION",
            customerId,
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
