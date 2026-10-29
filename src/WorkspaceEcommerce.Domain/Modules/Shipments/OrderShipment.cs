using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public sealed class OrderShipment : Entity
{
    private OrderShipment()
    {
    }

    public OrderShipment(
        Guid id,
        Guid orderId,
        string provider,
        Guid providerShipmentId,
        string trackingCode,
        string providerStatus,
        decimal shippingFeeAmount,
        string currency,
        DateTimeOffset observedAtUtc)
        : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order id is required for a shipment.");
        }

        if (providerShipmentId == Guid.Empty)
        {
            throw new DomainException("Provider shipment id is required.");
        }

        if (observedAtUtc == default)
        {
            throw new DomainException("Shipment observation timestamp is required.");
        }

        OrderId = orderId;
        Provider = Guard.Required(provider, nameof(Provider));
        ProviderShipmentId = providerShipmentId;
        TrackingCode = Guard.Required(trackingCode, nameof(TrackingCode));
        ProviderStatus = Guard.Required(providerStatus, nameof(ProviderStatus));
        ShippingFeeAmount = Guard.NotNegative(shippingFeeAmount, nameof(ShippingFeeAmount));
        ActualCostAmount = ShippingFeeAmount;
        Currency = Guard.Required(currency, nameof(Currency)).ToUpperInvariant();
        LastSyncedAtUtc = observedAtUtc;
        LastEventAtUtc = observedAtUtc;
        CreatedAtUtc = observedAtUtc;
        UpdatedAtUtc = observedAtUtc;
    }

    public Guid OrderId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public Guid? ProviderShipmentId { get; private set; }

    public string TrackingCode { get; private set; } = string.Empty;

    public string ProviderStatus { get; private set; } = string.Empty;

    // Legacy provider field retained for existing contracts. New money views use
    // order selection for the customer charge and ActualCostAmount for cost.
    public decimal ShippingFeeAmount { get; private set; }

    public decimal? ActualCostAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public DateTimeOffset LastSyncedAtUtc { get; private set; }

    public DateTimeOffset LastEventAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static OrderShipment CreateWorkspace(Guid id, Guid orderId, string trackingCode, DateTimeOffset now)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || now == default)
            throw new DomainException("Local shipment identifiers and timestamp are required.");
        return new OrderShipment
        {
            Id = id,
            OrderId = orderId,
            Provider = nameof(ShippingMethod.WorkspaceEC),
            TrackingCode = Guard.Required(trackingCode, nameof(TrackingCode)),
            ProviderStatus = "AwaitingRelease",
            Currency = "VND",
            LastSyncedAtUtc = now,
            LastEventAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public bool ApplyProviderState(
        string providerStatus,
        decimal shippingFeeAmount,
        string currency,
        DateTimeOffset syncedAtUtc,
        DateTimeOffset eventAtUtc)
    {
        if (Provider == nameof(ShippingMethod.WorkspaceEC))
            throw new DomainException("Partner state cannot be applied to a WorkspaceEC shipment.");
        if (syncedAtUtc == default || eventAtUtc == default)
        {
            throw new DomainException("Shipment synchronization timestamps are required.");
        }

        LastSyncedAtUtc = syncedAtUtc;
        UpdatedAtUtc = syncedAtUtc;

        if (eventAtUtc < LastEventAtUtc)
        {
            return false;
        }

        ProviderStatus = Guard.Required(providerStatus, nameof(ProviderStatus));
        ShippingFeeAmount = Guard.NotNegative(shippingFeeAmount, nameof(ShippingFeeAmount));
        ActualCostAmount = ShippingFeeAmount;
        Currency = Guard.Required(currency, nameof(Currency)).ToUpperInvariant();
        LastEventAtUtc = eventAtUtc;
        return true;
    }
}
