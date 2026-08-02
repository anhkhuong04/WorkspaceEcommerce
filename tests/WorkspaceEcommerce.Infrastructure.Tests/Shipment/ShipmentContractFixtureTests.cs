using System.Text.Json;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Tests.Shipment;

public sealed class ShipmentContractFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateShipmentResponse_ProviderFixture_DeserializesExpectedMapping()
    {
        var response = await ReadFixtureAsync<CreateShipmentResponse>("create-shipment-response.json");

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), response.ShipmentId);
        Assert.Equal("ORD-CONTRACT-1", response.ExternalOrderId);
        Assert.Equal("ML202608020001", response.TrackingCode);
        Assert.Equal("PendingPickup", response.Status);
        Assert.Equal(53000m, response.ShippingFeeAmount);
        Assert.Equal("VND", response.Currency);
    }

    [Fact]
    public async Task TrackingResponse_ProviderFixture_DeserializesTimelineAndCurrency()
    {
        var response = await ReadFixtureAsync<TrackingResponse>("tracking-response.json");

        Assert.Equal("Delivering", response.Status);
        Assert.Equal("VND", response.Currency);
        Assert.Equal(2, response.Timeline.Length);
        Assert.Equal("PendingPickup", response.Timeline[0].Status);
        Assert.Equal("Delivering", response.Timeline[1].Status);
        Assert.True(ShipmentProviderContract.IsKnownStatus(response.Status));
    }

    [Fact]
    public async Task WebhookPayload_ProviderFixture_UsesDocumentedSignaturePayloadFields()
    {
        var payload = await ReadFixtureAsync<ShipmentWebhookPayload>("webhook-status-changed.json");

        Assert.NotEqual(Guid.Empty, payload.EventId);
        Assert.Equal(ShipmentProviderContract.ShipmentStatusChangedEvent, payload.Event);
        Assert.Equal("ML202608020001", payload.TrackingCode);
        Assert.Equal("ORD-CONTRACT-1", payload.ExternalOrderId);
        Assert.Equal("DeliveryFailed", payload.Status);
        Assert.True(ShipmentProviderContract.IsKnownStatus(payload.Status));
    }

    private static async Task<T> ReadFixtureAsync<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Shipment", "Fixtures", fileName);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Fixture '{fileName}' could not be deserialized.");
    }
}
