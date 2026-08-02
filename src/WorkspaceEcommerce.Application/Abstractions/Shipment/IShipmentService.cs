namespace WorkspaceEcommerce.Application.Abstractions.Shipment;

public interface IShipmentService
{
    Task<ShippingQuoteResponse> GetShippingQuoteAsync(
        ShippingQuoteRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateShipmentResponse> CreateShipmentAsync(
        CreateShipmentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TrackingResponse> GetTrackingAsync(
        string trackingCode,
        CancellationToken cancellationToken = default);

    Task<TrackingResponse> CancelShipmentAsync(
        string trackingCode,
        string reason,
        CancellationToken cancellationToken = default);
}
