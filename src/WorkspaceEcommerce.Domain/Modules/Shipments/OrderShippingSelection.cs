using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

/// <summary>Immutable customer charge and quote snapshot, separate from carrier cost and legacy order IDs.</summary>
public sealed class OrderShippingSelection : Entity
{
    private OrderShippingSelection() { }
    public OrderShippingSelection(Guid id, ShippingQuote quote) : base(id)
    {
        if (!quote.OrderId.HasValue) throw new DomainException("A quote must be consumed before recording its selection.");
        OrderId = quote.OrderId.Value;
        QuoteId = quote.Id;
        Method = quote.Method;
        CustomerCharge = quote.CustomerCharge;
        WeightGrams = quote.WeightGrams;
        RatePlanId = quote.RatePlanId;
        SnapshotJson = quote.SnapshotJson;
    }
    public Guid OrderId { get; private set; }
    public Guid QuoteId { get; private set; }
    public ShippingMethod Method { get; private set; }
    public decimal CustomerCharge { get; private set; }
    public int WeightGrams { get; private set; }
    public Guid? RatePlanId { get; private set; }
    public string SnapshotJson { get; private set; } = string.Empty;
}
