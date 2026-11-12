using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Payments;

public sealed class OrderPaymentException : Entity
{
    public OrderPaymentException(Guid id, Guid orderId, Guid requestId, decimal expectedAmount, decimal actualAmount, string reference, string reason, string actor, DateTimeOffset recordedAt) : base(id)
    {
        if (orderId == Guid.Empty || requestId == Guid.Empty || expectedAmount == actualAmount || recordedAt == default) throw new DomainException("A payment difference, order and request ID are required.");
        OrderId = orderId; RequestId = requestId;
        ExpectedAmount = CommerceCurrency.RequireValidAmount(expectedAmount, nameof(ExpectedAmount));
        ActualAmount = CommerceCurrency.RequireValidAmount(actualAmount, nameof(ActualAmount));
        Reference = Guard.Required(reference, nameof(Reference)); Reason = Guard.Required(reason, nameof(Reason)); Actor = Guard.Required(actor, nameof(Actor)); RecordedAt = recordedAt;
    }
    public Guid OrderId { get; private set; }
    public Guid RequestId { get; private set; }
    public decimal ExpectedAmount { get; private set; }
    public decimal ActualAmount { get; private set; }
    public string Reference { get; private set; }
    public string Reason { get; private set; }
    public string Actor { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string? Settlement { get; private set; }
    public decimal? SettlementAmount { get; private set; }
    public string? SettlementReference { get; private set; }
    public string? SettlementReason { get; private set; }
    public string? SettledBy { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }
    public void Resolve(string settlement, decimal amount, string reference, string reason, string actor, DateTimeOffset now)
    {
        if (SettledAt.HasValue || now == default || amount <= 0) throw new DomainException("An open payment exception and positive verified settlement are required.");
        Settlement = Guard.Required(settlement, nameof(Settlement)); SettlementAmount = CommerceCurrency.RequireValidAmount(amount, nameof(SettlementAmount));
        SettlementReference = Guard.Required(reference, nameof(SettlementReference)); SettlementReason = Guard.Required(reason, nameof(SettlementReason));
        SettledBy = Guard.Required(actor, nameof(SettledBy)); SettledAt = now;
    }
}
