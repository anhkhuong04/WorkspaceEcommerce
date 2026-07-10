using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Loyalty;

namespace WorkspaceEcommerce.Application.Tests.Domain.Loyalty;

public sealed class LoyaltyTierTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesTier()
    {
        var tier = new LoyaltyTier(
            Guid.NewGuid(),
            LoyaltyTierType.Silver,
            minTotalPointsEarned: 500,
            discountPercent: 3m,
            freeShippingEnabled: false);

        Assert.Equal(LoyaltyTierType.Silver, tier.Type);
        Assert.Equal(500, tier.MinTotalPointsEarned);
        Assert.Equal(3m, tier.DiscountPercent);
        Assert.False(tier.FreeShippingEnabled);
    }

    [Fact]
    public void Constructor_NegativeMinimumPoints_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new LoyaltyTier(Guid.NewGuid(), LoyaltyTierType.Silver, -1, 3m, false));

        Assert.Equal("MinTotalPointsEarned cannot be negative.", exception.Message);
    }

    [Fact]
    public void Constructor_DiscountPercentGreaterThanOneHundred_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new LoyaltyTier(Guid.NewGuid(), LoyaltyTierType.Silver, 500, 101m, false));

        Assert.Equal("Loyalty tier discount percent cannot exceed 100.", exception.Message);
    }
}
