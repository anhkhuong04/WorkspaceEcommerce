using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class SerializedProductUnit : Entity
{
    public SerializedProductUnit(
        Guid id,
        Guid productVariantId,
        WarrantyIdentifierType identifierType,
        int identifierKeyVersion,
        string identifierFingerprint,
        string maskedIdentifier,
        Guid importBatchId,
        DateTimeOffset createdAt)
        : base(id)
    {
        if (productVariantId == Guid.Empty || importBatchId == Guid.Empty)
        {
            throw new DomainException("Product variant and import batch ids are required.");
        }

        if (!Enum.IsDefined(identifierType) || identifierKeyVersion < 1)
        {
            throw new DomainException("Serialized product unit identifier metadata is invalid.");
        }

        if (createdAt == default)
        {
            throw new DomainException("Serialized product unit creation timestamp is required.");
        }

        ProductVariantId = productVariantId;
        IdentifierType = identifierType;
        IdentifierKeyVersion = identifierKeyVersion;
        IdentifierFingerprint = Guard.Required(identifierFingerprint, nameof(IdentifierFingerprint));
        MaskedIdentifier = Guard.Required(maskedIdentifier, nameof(MaskedIdentifier));
        ImportBatchId = importBatchId;
        Status = SerializedProductUnitStatus.Available;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid ProductVariantId { get; private set; }

    public WarrantyIdentifierType IdentifierType { get; private set; }

    public int IdentifierKeyVersion { get; private set; }

    public string IdentifierFingerprint { get; private set; }

    public string MaskedIdentifier { get; private set; }

    public Guid ImportBatchId { get; private set; }

    public Guid? OrderItemId { get; private set; }

    public SerializedProductUnitStatus Status { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void AssignToOrderItem(Guid orderItemId, DateTimeOffset assignedAt)
    {
        if (Status != SerializedProductUnitStatus.Available || OrderItemId is not null)
        {
            throw new DomainException("Only an available product unit can be assigned to an order item.");
        }

        if (orderItemId == Guid.Empty || assignedAt == default)
        {
            throw new DomainException("Order item assignment is invalid.");
        }

        OrderItemId = orderItemId;
        AssignedAt = assignedAt;
        Status = SerializedProductUnitStatus.Assigned;
        Touch(assignedAt);
    }

    public void Activate(DateTimeOffset activatedAt)
    {
        if (Status is not (SerializedProductUnitStatus.Assigned or SerializedProductUnitStatus.Activated))
        {
            throw new DomainException("Only an assigned product unit can be activated.");
        }

        if (activatedAt == default)
        {
            throw new DomainException("Warranty activation timestamp is required.");
        }

        Status = SerializedProductUnitStatus.Activated;
        Touch(activatedAt);
    }

    public void Void(DateTimeOffset at)
    {
        if (Status is SerializedProductUnitStatus.Replaced or SerializedProductUnitStatus.Returned)
        {
            throw new DomainException("A replaced or returned product unit cannot be voided.");
        }

        Status = SerializedProductUnitStatus.Voided;
        Touch(at);
    }

    public void MarkReturned(DateTimeOffset at)
    {
        if (Status is SerializedProductUnitStatus.Voided or SerializedProductUnitStatus.Replaced)
        {
            throw new DomainException("A voided or replaced product unit cannot be returned.");
        }

        Status = SerializedProductUnitStatus.Returned;
        Touch(at);
    }

    public void MarkReplaced(DateTimeOffset at)
    {
        if (Status is SerializedProductUnitStatus.Voided or SerializedProductUnitStatus.Returned)
        {
            throw new DomainException("A voided or returned product unit cannot be replaced.");
        }

        Status = SerializedProductUnitStatus.Replaced;
        Touch(at);
    }

    private void Touch(DateTimeOffset at)
    {
        UpdatedAt = at == default ? DateTimeOffset.UtcNow : at;
    }
}
