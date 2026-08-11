using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class WarrantyAuditEvent : Entity
{
    public WarrantyAuditEvent(
        Guid id,
        Guid? warrantyEntitlementId,
        Guid? serializedProductUnitId,
        WarrantyAuditAction action,
        string actorType,
        string actorId,
        string? reason,
        string correlationId,
        DateTimeOffset occurredAt)
        : base(id)
    {
        if (warrantyEntitlementId is null && serializedProductUnitId is null)
        {
            throw new DomainException("Warranty audit event requires a unit or entitlement.");
        }

        if (!Enum.IsDefined(action) || occurredAt == default)
        {
            throw new DomainException("Warranty audit event values are invalid.");
        }

        WarrantyEntitlementId = warrantyEntitlementId;
        SerializedProductUnitId = serializedProductUnitId;
        Action = action;
        ActorType = Guard.Required(actorType, nameof(ActorType));
        ActorId = Guard.Required(actorId, nameof(ActorId));
        Reason = Guard.Optional(reason);
        CorrelationId = Guard.Required(correlationId, nameof(CorrelationId));
        OccurredAt = occurredAt;
    }

    public Guid? WarrantyEntitlementId { get; private set; }

    public Guid? SerializedProductUnitId { get; private set; }

    public WarrantyAuditAction Action { get; private set; }

    public string ActorType { get; private set; }

    public string ActorId { get; private set; }

    public string? Reason { get; private set; }

    public string CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
