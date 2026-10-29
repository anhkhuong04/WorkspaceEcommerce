using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Application.Abstractions.Shipment;

namespace WorkspaceEcommerce.Infrastructure.Shipment;

// Construct the HTTP adapter only for actual partner work. Intake configuration
// never disables the adapter: existing external orders still need callbacks/retries.
internal sealed class DeferredShipmentService(IServiceProvider services) : IShipmentService
{
    private IShipmentService Client => services.GetRequiredService<MiniLogisticsClient>();
    public Task<ShippingQuoteResponse> GetShippingQuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default) => Client.GetShippingQuoteAsync(request, cancellationToken);
    public Task<CreateShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, string idempotencyKey, CancellationToken cancellationToken = default) => Client.CreateShipmentAsync(request, idempotencyKey, cancellationToken);
    public Task<TrackingResponse> GetTrackingAsync(string trackingCode, CancellationToken cancellationToken = default) => Client.GetTrackingAsync(trackingCode, cancellationToken);
    public Task<TrackingResponse> CancelShipmentAsync(string trackingCode, string reason, CancellationToken cancellationToken = default) => Client.CancelShipmentAsync(trackingCode, reason, cancellationToken);
    public Task<TrackingResponse> CancelShipmentAsync(string trackingCode, string reason, string idempotencyKey, CancellationToken cancellationToken = default) => Client.CancelShipmentAsync(trackingCode, reason, idempotencyKey, cancellationToken);
}
