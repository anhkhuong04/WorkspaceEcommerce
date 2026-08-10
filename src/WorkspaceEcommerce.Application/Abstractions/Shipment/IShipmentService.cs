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

    /// <summary>
    /// Provider cancellation with a stable command idempotency key. Existing
    /// test/dummy providers may use the legacy overload; the production client
    /// must send this key to MiniLogistics.
    /// </summary>
    Task<TrackingResponse> CancelShipmentAsync(
        string trackingCode,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        CancelShipmentAsync(trackingCode, reason, cancellationToken);
}
