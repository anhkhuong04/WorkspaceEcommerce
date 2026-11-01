using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public enum WorkspaceShipmentAction { Assign = 0, Dispatch = 1, Deliver = 2, FailDelivery = 3, RetryDelivery = 4, Cancel = 5 }

public sealed class WorkspaceShipmentOperation : Entity
{
    private WorkspaceShipmentOperation() { }
    public WorkspaceShipmentOperation(Guid id, Guid shipmentId, WorkspaceShipmentAction action, string payloadHash,
        long revision, string status, string actor, string reason, string? assignedTo, DateTimeOffset now) : base(id)
    {
        if (id == Guid.Empty || shipmentId == Guid.Empty || !Enum.IsDefined(action) || revision <= 0 || now == default)
            throw new DomainException("Workspace shipment operation identifiers, revision and time are required.");
        ShipmentId = shipmentId; Action = action; Revision = revision;
        PayloadHash = ShippingRatePlan.Required(payloadHash, 64, nameof(PayloadHash));
        Status = ShippingRatePlan.Required(status, 50, nameof(Status));
        Actor = ShippingRatePlan.Required(actor, 256, nameof(Actor));
        Reason = ShippingRatePlan.Required(reason, 500, nameof(Reason));
        AssignedTo = assignedTo; OccurredAtUtc = now;
    }
    public Guid ShipmentId { get; private set; }
    public WorkspaceShipmentAction Action { get; private set; }
    public string PayloadHash { get; private set; } = string.Empty;
    public long Revision { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string? AssignedTo { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
