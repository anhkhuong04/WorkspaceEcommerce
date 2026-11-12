using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Catalog;

public sealed class StockAdjustment(Guid id, Guid productVariantId, int beforeQuantity, int afterQuantity, string reason, string actor, DateTimeOffset occurredAt) : Entity(id)
{
    public Guid ProductVariantId { get; private set; } = productVariantId;
    public int BeforeQuantity { get; private set; } = Guard.NotNegative(beforeQuantity, nameof(beforeQuantity));
    public int AfterQuantity { get; private set; } = Guard.NotNegative(afterQuantity, nameof(afterQuantity));
    public string Reason { get; private set; } = Guard.Required(reason, nameof(reason));
    public string Actor { get; private set; } = Guard.Required(actor, nameof(actor));
    public DateTimeOffset OccurredAt { get; private set; } = occurredAt;
}
