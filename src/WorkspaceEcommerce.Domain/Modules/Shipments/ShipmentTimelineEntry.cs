using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public sealed class ShipmentTimelineEntry : Entity
{
    private ShipmentTimelineEntry()
    {
    }

    public ShipmentTimelineEntry(
        Guid id,
        Guid orderShipmentId,
        string providerStatus,
        string? note,
        DateTimeOffset changedAtUtc,
        ShipmentTimelineSource source,
        Guid? providerEventId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (orderShipmentId == Guid.Empty)
        {
            throw new DomainException("Order shipment id is required for a timeline entry.");
        }

        if (changedAtUtc == default || createdAtUtc == default)
        {
            throw new DomainException("Shipment timeline timestamps are required.");
        }

        OrderShipmentId = orderShipmentId;
        ProviderStatus = Guard.Required(providerStatus, nameof(ProviderStatus));
        Note = Guard.Optional(note);
        ChangedAtUtc = changedAtUtc;
        Source = source;
        ProviderEventId = providerEventId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid OrderShipmentId { get; private set; }

    public string ProviderStatus { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public ShipmentTimelineSource Source { get; private set; }

    public Guid? ProviderEventId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public enum ShipmentTimelineSource
{
    ShipmentCreated = 0,
    Webhook = 1,
    LiveRefresh = 2,
    Cancellation = 3
}
