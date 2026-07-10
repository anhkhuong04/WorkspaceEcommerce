using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Loyalty;

namespace WorkspaceEcommerce.Application.Tests.Domain.Loyalty;

public sealed class CustomerLoyaltyAccountTests
{
    [Fact]
    public void Constructor_ValidCustomer_CreatesBronzeAccountWithZeroPoints()
    {
        var account = CreateAccount();

        Assert.Equal(0, account.CurrentPoints);
        Assert.Equal(0, account.TotalPointsEarned);
        Assert.Equal(LoyaltyTierType.Bronze, account.CurrentTier);
        Assert.Empty(account.Transactions);
    }

    [Fact]
    public void Constructor_EmptyCustomerId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new CustomerLoyaltyAccount(Guid.NewGuid(), Guid.Empty));

        Assert.Equal("Loyalty account customer id cannot be empty.", exception.Message);
    }

    [Fact]
    public void EarnPoints_ValidOrder_AddsEarnTransactionAndUpdatesBalances()
    {
        var account = CreateAccount();
        var orderId = Guid.NewGuid();

        var transaction = account.EarnPoints(15, orderId, "Earned from order.");

        Assert.Equal(15, account.CurrentPoints);
        Assert.Equal(15, account.TotalPointsEarned);
        Assert.Equal(LoyaltyTransactionType.Earn, transaction.Type);
        Assert.Equal(orderId, transaction.OrderId);
        Assert.Null(transaction.VoucherId);
        Assert.Equal(15, transaction.Points);
        Assert.Equal(15, transaction.BalanceAfter);
        Assert.Single(account.Transactions);
    }

    [Fact]
    public void EarnPoints_NonPositivePoints_ThrowsDomainException()
    {
        var account = CreateAccount();

        var exception = Assert.Throws<DomainException>(() =>
            account.EarnPoints(0, Guid.NewGuid(), "Earned from order."));

        Assert.Equal("Loyalty earn points must be greater than zero.", exception.Message);
    }

    [Fact]
    public void EarnPoints_EmptyOrderId_ThrowsDomainException()
    {
        var account = CreateAccount();

        var exception = Assert.Throws<DomainException>(() =>
            account.EarnPoints(10, Guid.Empty, "Earned from order."));

        Assert.Equal("Loyalty earn order id cannot be empty.", exception.Message);
    }

    [Fact]
    public void EarnPoints_DuplicateOrder_ThrowsDomainException()
    {
        var account = CreateAccount();
        var orderId = Guid.NewGuid();
        account.EarnPoints(10, orderId, "Earned from order.");

        var exception = Assert.Throws<DomainException>(() =>
            account.EarnPoints(10, orderId, "Duplicate earn."));

        Assert.Equal("Loyalty points have already been earned for this order.", exception.Message);
    }

    [Fact]
    public void RedeemPoints_ValidVoucher_AddsRedeemTransactionAndKeepsTotalEarned()
    {
        var account = CreateAccount();
        account.EarnPoints(150, Guid.NewGuid(), "Earned from order.");
        var voucherId = Guid.NewGuid();

        var transaction = account.RedeemPoints(100, voucherId, "Redeemed for voucher.");

        Assert.Equal(50, account.CurrentPoints);
        Assert.Equal(150, account.TotalPointsEarned);
        Assert.Equal(LoyaltyTransactionType.Redeem, transaction.Type);
        Assert.Null(transaction.OrderId);
        Assert.Equal(voucherId, transaction.VoucherId);
        Assert.Equal(-100, transaction.Points);
        Assert.Equal(50, transaction.BalanceAfter);
        Assert.Equal(2, account.Transactions.Count);
    }

    [Fact]
    public void RedeemPoints_NonPositivePoints_ThrowsDomainException()
    {
        var account = CreateAccount();

        var exception = Assert.Throws<DomainException>(() =>
            account.RedeemPoints(0, Guid.NewGuid(), "Redeemed for voucher."));

        Assert.Equal("Loyalty redeem points must be greater than zero.", exception.Message);
    }

    [Fact]
    public void RedeemPoints_EmptyVoucherId_ThrowsDomainException()
    {
        var account = CreateAccount();
        account.EarnPoints(100, Guid.NewGuid(), "Earned from order.");

        var exception = Assert.Throws<DomainException>(() =>
            account.RedeemPoints(50, Guid.Empty, "Redeemed for voucher."));

        Assert.Equal("Loyalty redeem voucher id cannot be empty.", exception.Message);
    }

    [Fact]
    public void RedeemPoints_WhenBalanceIsInsufficient_ThrowsDomainException()
    {
        var account = CreateAccount();
        account.EarnPoints(50, Guid.NewGuid(), "Earned from order.");

        var exception = Assert.Throws<DomainException>(() =>
            account.RedeemPoints(100, Guid.NewGuid(), "Redeemed for voucher."));

        Assert.Equal("Loyalty account does not have enough points.", exception.Message);
        Assert.Equal(50, account.CurrentPoints);
        Assert.Single(account.Transactions);
    }

    [Fact]
    public void RedeemPoints_DuplicateVoucher_ThrowsDomainException()
    {
        var account = CreateAccount();
        account.EarnPoints(200, Guid.NewGuid(), "Earned from order.");
        var voucherId = Guid.NewGuid();
        account.RedeemPoints(50, voucherId, "Redeemed for voucher.");

        var exception = Assert.Throws<DomainException>(() =>
            account.RedeemPoints(50, voucherId, "Duplicate redeem."));

        Assert.Equal("Loyalty points have already been redeemed for this voucher.", exception.Message);
    }

    [Fact]
    public void TryEvaluateTierUpgrade_WhenEligible_UpgradesToHighestMatchingTier()
    {
        var account = CreateAccount();
        account.EarnPoints(2500, Guid.NewGuid(), "Earned from order.");

        var upgraded = account.TryEvaluateTierUpgrade(CreateDefaultTiers());

        Assert.True(upgraded);
        Assert.Equal(LoyaltyTierType.Gold, account.CurrentTier);
    }

    [Fact]
    public void TryEvaluateTierUpgrade_WhenNoHigherTierIsEligible_ReturnsFalse()
    {
        var account = CreateAccount();
        account.EarnPoints(600, Guid.NewGuid(), "Earned from order.");
        account.TryEvaluateTierUpgrade(CreateDefaultTiers());

        var upgraded = account.TryEvaluateTierUpgrade(
            [CreateTier(LoyaltyTierType.Bronze, 0)]);

        Assert.False(upgraded);
        Assert.Equal(LoyaltyTierType.Silver, account.CurrentTier);
    }

    [Fact]
    public void TryEvaluateTierUpgrade_WhenNoTierMatches_ReturnsFalse()
    {
        var account = CreateAccount();

        var upgraded = account.TryEvaluateTierUpgrade(
            [CreateTier(LoyaltyTierType.Silver, 500)]);

        Assert.False(upgraded);
        Assert.Equal(LoyaltyTierType.Bronze, account.CurrentTier);
    }

    private static CustomerLoyaltyAccount CreateAccount()
    {
        return new CustomerLoyaltyAccount(Guid.NewGuid(), Guid.NewGuid());
    }

    private static LoyaltyTier[] CreateDefaultTiers()
    {
        return
        [
            CreateTier(LoyaltyTierType.Bronze, 0),
            CreateTier(LoyaltyTierType.Silver, 500),
            CreateTier(LoyaltyTierType.Gold, 2000),
            CreateTier(LoyaltyTierType.Platinum, 5000)
        ];
    }

    private static LoyaltyTier CreateTier(LoyaltyTierType type, int minTotalPointsEarned)
    {
        return new LoyaltyTier(
            Guid.NewGuid(),
            type,
            minTotalPointsEarned,
            discountPercent: 0m,
            freeShippingEnabled: false);
    }
}
