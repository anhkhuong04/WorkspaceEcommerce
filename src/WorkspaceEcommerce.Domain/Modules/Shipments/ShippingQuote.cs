using System.Text.Json;
using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

/// <summary>Server-owned offer, consumed atomically with an accepted order.</summary>
public sealed class ShippingQuote : Entity
{
    private ShippingQuote() { }
    public ShippingQuote(Guid id, ShippingMethod method, string subjectHash, string inputFingerprint,
        decimal customerCharge, int weightGrams, string snapshotJson, Guid? ratePlanId,
        DateTimeOffset createdAt, DateTimeOffset expiresAt) : base(id)
    {
        if (!Enum.IsDefined(method) || weightGrams <= 0 || createdAt == default || expiresAt <= createdAt)
            throw new DomainException("Shipping quote method, weight or validity is invalid.");
        ShippingRateZone.ValidateMoney(customerCharge);
        using var snapshot = JsonDocument.Parse(snapshotJson);
        Method = method;
        SubjectHash = ShippingRatePlan.Required(subjectHash, 64, nameof(SubjectHash));
        InputFingerprint = ShippingRatePlan.Required(inputFingerprint, 64, nameof(InputFingerprint));
        CustomerCharge = customerCharge;
        WeightGrams = weightGrams;
        SnapshotJson = snapshotJson;
        RatePlanId = ratePlanId;
        CreatedAtUtc = createdAt;
        ExpiresAtUtc = expiresAt;
        Revision = 1;
    }
    public ShippingMethod Method { get; private set; }
    public string SubjectHash { get; private set; } = string.Empty;
    public string InputFingerprint { get; private set; } = string.Empty;
    public decimal CustomerCharge { get; private set; }
    public int WeightGrams { get; private set; }
    public string SnapshotJson { get; private set; } = string.Empty;
    public Guid? RatePlanId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? OrderId { get; private set; }
    public long Revision { get; private set; }
    public string? CheckoutRequestHash { get; private set; }
    public string? RevocationActor { get; private set; }
    public string? RevocationReason { get; private set; }

    public void Consume(Guid orderId, DateTimeOffset now, string? checkoutRequestHash = null)
    {
        if (orderId == Guid.Empty || OrderId.HasValue || RevokedAtUtc.HasValue || now < CreatedAtUtc || now >= ExpiresAtUtc)
            throw new DomainException("Shipping quote is not available for consumption.");
        OrderId = orderId;
        CheckoutRequestHash = checkoutRequestHash is null ? null : ShippingRatePlan.Required(checkoutRequestHash, 64, nameof(CheckoutRequestHash));
        Revision++;
    }

    public void Revoke(DateTimeOffset now, string? actor = null, string? reason = null)
    {
        if (OrderId.HasValue || now < CreatedAtUtc)
            throw new DomainException("A consumed quote cannot be revoked.");
        RevokedAtUtc = now;
        RevocationActor = actor is null ? null : ShippingRatePlan.Required(actor, 256, nameof(RevocationActor));
        RevocationReason = reason is null ? null : ShippingRatePlan.Required(reason, 500, nameof(RevocationReason));
        Revision++;
    }
}
