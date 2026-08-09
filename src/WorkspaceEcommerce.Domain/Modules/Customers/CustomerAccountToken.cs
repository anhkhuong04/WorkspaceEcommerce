using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

/// <summary>
/// A one-time customer account token. Only a SHA-256 digest is persisted; the
/// original value is delivered through the protected email outbox payload.
/// </summary>
public sealed class CustomerAccountToken : Entity
{
    private CustomerAccountToken()
    {
    }

    public CustomerAccountToken(
        Guid id,
        Guid customerId,
        CustomerAccountTokenPurpose purpose,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id is required for an account token.");
        }

        if (expiresAt <= createdAt)
        {
            throw new DomainException("Account token expiry must be after creation.");
        }

        CustomerId = customerId;
        Purpose = purpose;
        TokenHash = Guard.Required(tokenHash, nameof(tokenHash));
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid CustomerId { get; private set; }

    public CustomerAccountTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsActiveAt(DateTimeOffset timestamp) => ConsumedAt is null && ExpiresAt > timestamp;

    public void Consume(DateTimeOffset consumedAt)
    {
        if (ConsumedAt.HasValue)
        {
            throw new DomainException("An account token may only be consumed once.");
        }

        ConsumedAt = consumedAt;
    }
}

public enum CustomerAccountTokenPurpose
{
    EmailVerification = 0,
    PasswordReset = 1
}
