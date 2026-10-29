using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public sealed class ShippingRateAudit : Entity
{
    private ShippingRateAudit() { }
    public ShippingRateAudit(Guid id, Guid ratePlanId, long revision, string action,
        string actor, string reason, string changesJson, DateTimeOffset now) : base(id)
    {
        RatePlanId = ratePlanId;
        Revision = revision;
        Action = ShippingRatePlan.Required(action, 30, nameof(Action));
        Actor = ShippingRatePlan.Required(actor, 256, nameof(Actor));
        Reason = ShippingRatePlan.Required(reason, 500, nameof(Reason));
        ChangesJson = ShippingRatePlan.Required(changesJson, 200_000, nameof(ChangesJson));
        OccurredAtUtc = now;
    }
    public Guid RatePlanId { get; private set; }
    public long Revision { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string ChangesJson { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
