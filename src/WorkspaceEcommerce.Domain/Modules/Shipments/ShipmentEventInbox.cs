using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public sealed class ShipmentEventInbox : Entity
{
    private ShipmentEventInbox()
    {
    }

    public ShipmentEventInbox(
        Guid eventId,
        string eventName,
        string trackingCode,
        string externalOrderId,
        string providerStatus,
        DateTimeOffset changedAtUtc,
        DateTimeOffset receivedAtUtc)
        : base(eventId)
    {
        if (changedAtUtc == default || receivedAtUtc == default)
        {
            throw new DomainException("Shipment event timestamps are required.");
        }

        EventName = Guard.Required(eventName, nameof(EventName));
        TrackingCode = Guard.Required(trackingCode, nameof(TrackingCode));
        ExternalOrderId = Guard.Required(externalOrderId, nameof(ExternalOrderId));
        ProviderStatus = Guard.Required(providerStatus, nameof(ProviderStatus));
        ChangedAtUtc = changedAtUtc;
        ReceivedAtUtc = receivedAtUtc;
    }

    public string EventName { get; private set; } = string.Empty;

    public string TrackingCode { get; private set; } = string.Empty;

    public string ExternalOrderId { get; private set; } = string.Empty;

    public string ProviderStatus { get; private set; } = string.Empty;

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public string? ProcessingError { get; private set; }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        if (processedAtUtc == default)
        {
            throw new DomainException("Shipment event processed timestamp is required.");
        }

        ProcessedAtUtc = processedAtUtc;
        ProcessingError = null;
    }

    public void MarkFailed(string error)
    {
        ProcessingError = Guard.Required(error, nameof(error));
    }
}
