using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class CustomerTwoFactorChallenge : Entity
{
    public CustomerTwoFactorChallenge(
        Guid id,
        Guid customerId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
        : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id cannot be empty.");
        }

        if (expiresAt <= createdAt)
        {
            throw new DomainException("Two-factor challenge expiry must be after creation.");
        }

        CustomerId = customerId;
        TokenHash = Guard.Required(tokenHash, nameof(tokenHash));
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    private CustomerTwoFactorChallenge()
    {
    }

    public Guid CustomerId { get; private set; }

    public string TokenHash { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsActiveAt(DateTimeOffset timestamp) =>
        !ConsumedAt.HasValue && ExpiresAt > timestamp;

    public void Consume(DateTimeOffset consumedAt)
    {
        if (!IsActiveAt(consumedAt))
        {
            throw new DomainException("Two-factor challenge is no longer active.");
        }

        ConsumedAt = consumedAt;
    }
}
