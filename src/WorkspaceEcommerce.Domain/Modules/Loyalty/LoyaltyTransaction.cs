using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Loyalty;

public sealed class LoyaltyTransaction : Entity
{
    public LoyaltyTransaction(
        Guid id,
        Guid customerLoyaltyAccountId,
        Guid? orderId,
        Guid? voucherId,
        LoyaltyTransactionType type,
        int points,
        int balanceAfter,
        string description,
        DateTimeOffset createdAt = default)
        : base(id)
    {
        if (customerLoyaltyAccountId == Guid.Empty)
        {
            throw new DomainException("Loyalty transaction account id cannot be empty.");
        }

        CustomerLoyaltyAccountId = customerLoyaltyAccountId;
        OrderId = NormalizeOptionalId(orderId, "Loyalty transaction order id cannot be empty.");
        VoucherId = NormalizeOptionalId(voucherId, "Loyalty transaction voucher id cannot be empty.");
        Type = type;
        Points = ValidatePoints(type, points, OrderId, VoucherId);
        BalanceAfter = Guard.NotNegative(balanceAfter, nameof(BalanceAfter));
        Description = Guard.Required(description, nameof(Description));
        CreatedAt = createdAt == default ? DateTimeOffset.UtcNow : createdAt;
    }

    public Guid CustomerLoyaltyAccountId { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? VoucherId { get; private set; }

    public LoyaltyTransactionType Type { get; private set; }

    public int Points { get; private set; }

    public int BalanceAfter { get; private set; }

    public string Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private static int ValidatePoints(
        LoyaltyTransactionType type,
        int points,
        Guid? orderId,
        Guid? voucherId)
    {
        return type switch
        {
            LoyaltyTransactionType.Earn => ValidateEarnPoints(points, orderId),
            LoyaltyTransactionType.Redeem => ValidateRedeemPoints(points, voucherId),
            LoyaltyTransactionType.Adjust => ValidateAdjustPoints(points),
            _ => throw new DomainException("Loyalty transaction type is not supported.")
        };
    }

    private static int ValidateEarnPoints(int points, Guid? orderId)
    {
        if (orderId is null)
        {
            throw new DomainException("Loyalty earn transaction order id is required.");
        }

        if (points <= 0)
        {
            throw new DomainException("Loyalty earn points must be greater than zero.");
        }

        return points;
    }

    private static int ValidateRedeemPoints(int points, Guid? voucherId)
    {
        if (voucherId is null)
        {
            throw new DomainException("Loyalty redeem transaction voucher id is required.");
        }

        if (points >= 0)
        {
            throw new DomainException("Loyalty redeem points must be less than zero.");
        }

        return points;
    }

    private static int ValidateAdjustPoints(int points)
    {
        if (points == 0)
        {
            throw new DomainException("Loyalty adjust points cannot be zero.");
        }

        return points;
    }

    private static Guid? NormalizeOptionalId(Guid? id, string emptyMessage)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(emptyMessage);
        }

        return id;
    }
}
