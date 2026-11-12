using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Loyalty;

public sealed class CustomerLoyaltyAccount : Entity
{
    private readonly List<LoyaltyTransaction> _transactions = [];

    public CustomerLoyaltyAccount(Guid id, Guid customerId)
        : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Loyalty account customer id cannot be empty.");
        }

        CustomerId = customerId;
        CurrentPoints = 0;
        TotalPointsEarned = 0;
        CurrentTier = LoyaltyTierType.Bronze;
        TierUpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CustomerId { get; private set; }

    public int CurrentPoints { get; private set; }
    public int PointsDebt { get; private set; }

    public LoyaltyTransaction ReverseEarnedPoints(int points, Guid orderId, string description, IEnumerable<LoyaltyTier> tiers, DateTimeOffset now)
    {
        if (points <= 0 || orderId == Guid.Empty) throw new DomainException("Positive reversal points and order are required.");
        var fromBalance = Math.Min(points, CurrentPoints);
        CurrentPoints -= fromBalance; PointsDebt = checked(PointsDebt + points - fromBalance);
        TotalPointsEarned = Math.Max(0, TotalPointsEarned - points);
        var tier = tiers.Where(t => t.MinTotalPointsEarned <= TotalPointsEarned).OrderByDescending(t => t.MinTotalPointsEarned).ThenByDescending(t => t.Type).FirstOrDefault();
        if (tier is not null) { CurrentTier = tier.Type; TierUpdatedAt = now; }
        var transaction = new LoyaltyTransaction(Guid.NewGuid(), Id, orderId, null, LoyaltyTransactionType.Adjust, -points, CurrentPoints, description);
        _transactions.Add(transaction); return transaction;
    }

    public int TotalPointsEarned { get; private set; }

    public LoyaltyTierType CurrentTier { get; private set; }

    public DateTimeOffset TierUpdatedAt { get; private set; }

    public IReadOnlyCollection<LoyaltyTransaction> Transactions => _transactions;

    public LoyaltyTransaction EarnPoints(int points, Guid orderId, string description)
    {
        if (points <= 0)
        {
            throw new DomainException("Loyalty earn points must be greater than zero.");
        }

        if (orderId == Guid.Empty)
        {
            throw new DomainException("Loyalty earn order id cannot be empty.");
        }

        if (_transactions.Any(transaction =>
                transaction.Type == LoyaltyTransactionType.Earn &&
                transaction.OrderId == orderId))
        {
            throw new DomainException("Loyalty points have already been earned for this order.");
        }

        var debtPayment = Math.Min(points, PointsDebt);
        PointsDebt -= debtPayment;
        CurrentPoints += points - debtPayment;
        TotalPointsEarned += points;

        var transaction = new LoyaltyTransaction(
            Guid.NewGuid(),
            Id,
            orderId,
            voucherId: null,
            LoyaltyTransactionType.Earn,
            points,
            CurrentPoints,
            description);

        _transactions.Add(transaction);

        return transaction;
    }

    public LoyaltyTransaction RedeemPoints(int points, Guid voucherId, string description)
    {
        if (points <= 0)
        {
            throw new DomainException("Loyalty redeem points must be greater than zero.");
        }

        if (voucherId == Guid.Empty)
        {
            throw new DomainException("Loyalty redeem voucher id cannot be empty.");
        }

        if (points > CurrentPoints)
        {
            throw new DomainException("Loyalty account does not have enough points.");
        }

        if (_transactions.Any(transaction =>
                transaction.Type == LoyaltyTransactionType.Redeem &&
                transaction.VoucherId == voucherId))
        {
            throw new DomainException("Loyalty points have already been redeemed for this voucher.");
        }

        CurrentPoints -= points;

        var transaction = new LoyaltyTransaction(
            Guid.NewGuid(),
            Id,
            orderId: null,
            voucherId,
            LoyaltyTransactionType.Redeem,
            -points,
            CurrentPoints,
            description);

        _transactions.Add(transaction);

        return transaction;
    }

    public bool TryEvaluateTierUpgrade(IEnumerable<LoyaltyTier> tierDefinitions)
    {
        var nextTier = tierDefinitions
            .Where(tier => tier.MinTotalPointsEarned <= TotalPointsEarned)
            .OrderByDescending(tier => tier.MinTotalPointsEarned)
            .ThenByDescending(tier => tier.Type)
            .FirstOrDefault();

        if (nextTier is null || nextTier.Type <= CurrentTier)
        {
            return false;
        }

        CurrentTier = nextTier.Type;
        TierUpdatedAt = DateTimeOffset.UtcNow;

        return true;
    }
}
