using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class ProductVariantWarrantyPlan : Entity
{
    public ProductVariantWarrantyPlan(
        Guid id,
        Guid productVariantId,
        Guid warrantyPlanId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo = null)
        : base(id)
    {
        if (productVariantId == Guid.Empty || warrantyPlanId == Guid.Empty)
        {
            throw new DomainException("Product variant and warranty plan ids are required.");
        }

        if (effectiveFrom == default || (effectiveTo is not null && effectiveTo <= effectiveFrom))
        {
            throw new DomainException("Warranty plan assignment effective window is invalid.");
        }

        ProductVariantId = productVariantId;
        WarrantyPlanId = warrantyPlanId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ProductVariantId { get; private set; }

    public Guid WarrantyPlanId { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsEffectiveAt(DateTimeOffset at) => at >= EffectiveFrom && (EffectiveTo is null || at <= EffectiveTo.Value);
}
