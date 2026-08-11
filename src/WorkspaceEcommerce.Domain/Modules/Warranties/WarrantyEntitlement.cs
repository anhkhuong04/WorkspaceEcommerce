using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class WarrantyEntitlement : Entity
{
    private readonly List<WarrantyCoverageSnapshot> _coverageSnapshots = [];

    public WarrantyEntitlement(
        Guid id,
        Guid serializedProductUnitId,
        Guid warrantyPlanId,
        Guid orderId,
        Guid orderItemId,
        Guid? customerId,
        DateTimeOffset createdAt)
        : base(id)
    {
        if (serializedProductUnitId == Guid.Empty || warrantyPlanId == Guid.Empty || orderId == Guid.Empty || orderItemId == Guid.Empty)
        {
            throw new DomainException("Warranty entitlement linkage is required.");
        }

        if (customerId == Guid.Empty || createdAt == default)
        {
            throw new DomainException("Warranty entitlement creation values are invalid.");
        }

        SerializedProductUnitId = serializedProductUnitId;
        WarrantyPlanId = warrantyPlanId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        CustomerId = customerId;
        Status = WarrantyEntitlementStatus.PendingActivation;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid SerializedProductUnitId { get; private set; }

    public Guid WarrantyPlanId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid OrderItemId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public DateTimeOffset? PurchasedAt { get; private set; }

    public DateTimeOffset? EligibleAt { get; private set; }

    public DateTimeOffset? ActivationDeadline { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public WarrantyEntitlementStatus Status { get; private set; }

    public WarrantyActivationSource? ActivationSource { get; private set; }

    public string? AcceptedTermsVersion { get; private set; }

    public Guid? ReplacementSerializedProductUnitId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<WarrantyCoverageSnapshot> CoverageSnapshots => _coverageSnapshots;

    public void Activate(
        DateTimeOffset purchasedAt,
        DateTimeOffset eligibleAt,
        DateTimeOffset activationDeadline,
        DateTimeOffset activatedAt,
        WarrantyActivationSource source,
        string acceptedTermsVersion,
        IEnumerable<WarrantyCoverageSnapshot> coverageSnapshots)
    {
        if (Status == WarrantyEntitlementStatus.Active)
        {
            return;
        }

        if (Status != WarrantyEntitlementStatus.PendingActivation)
        {
            throw new DomainException("Only a pending warranty entitlement can be activated.");
        }

        if (purchasedAt == default || eligibleAt == default || activationDeadline == default || activatedAt == default ||
            activationDeadline < eligibleAt || activatedAt > activationDeadline || !Enum.IsDefined(source))
        {
            throw new DomainException("Warranty activation dates or source are invalid.");
        }

        var snapshots = coverageSnapshots?.ToArray() ?? [];
        if (snapshots.Length == 0 || snapshots.Any(snapshot => snapshot.WarrantyEntitlementId != Id))
        {
            throw new DomainException("Warranty activation requires coverage snapshots for this entitlement.");
        }

        PurchasedAt = purchasedAt;
        EligibleAt = eligibleAt;
        ActivationDeadline = activationDeadline;
        ActivatedAt = activatedAt;
        ActivationSource = source;
        AcceptedTermsVersion = Guard.Required(acceptedTermsVersion, nameof(acceptedTermsVersion));
        _coverageSnapshots.AddRange(snapshots);
        Status = WarrantyEntitlementStatus.Active;
        UpdatedAt = activatedAt;
    }

    public void Void(DateTimeOffset at)
    {
        if (Status is WarrantyEntitlementStatus.Voided or WarrantyEntitlementStatus.Replaced)
        {
            throw new DomainException("Warranty entitlement is already terminal.");
        }

        if (at == default)
        {
            throw new DomainException("Warranty void timestamp is required.");
        }

        Status = WarrantyEntitlementStatus.Voided;
        UpdatedAt = at;
    }

    public void MarkReplaced(Guid replacementSerializedProductUnitId, DateTimeOffset at)
    {
        if (Status != WarrantyEntitlementStatus.Active || replacementSerializedProductUnitId == Guid.Empty ||
            replacementSerializedProductUnitId == SerializedProductUnitId || at == default)
        {
            throw new DomainException("Only an active warranty entitlement can be replaced.");
        }

        ReplacementSerializedProductUnitId = replacementSerializedProductUnitId;
        Status = WarrantyEntitlementStatus.Replaced;
        UpdatedAt = at;
    }
}
