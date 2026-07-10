using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Loyalty;

public sealed class LoyaltyTier : Entity
{
    public LoyaltyTier(
        Guid id,
        LoyaltyTierType type,
        int minTotalPointsEarned,
        decimal discountPercent,
        bool freeShippingEnabled)
        : base(id)
    {
        Type = type;
        MinTotalPointsEarned = Guard.NotNegative(minTotalPointsEarned, nameof(MinTotalPointsEarned));
        DiscountPercent = ValidateDiscountPercent(discountPercent);
        FreeShippingEnabled = freeShippingEnabled;
    }

    public LoyaltyTierType Type { get; private set; }

    public int MinTotalPointsEarned { get; private set; }

    public decimal DiscountPercent { get; private set; }

    public bool FreeShippingEnabled { get; private set; }

    private static decimal ValidateDiscountPercent(decimal discountPercent)
    {
        var normalizedDiscountPercent = Guard.NotNegative(discountPercent, nameof(DiscountPercent));
        if (normalizedDiscountPercent > 100m)
        {
            throw new DomainException("Loyalty tier discount percent cannot exceed 100.");
        }

        return normalizedDiscountPercent;
    }
}
