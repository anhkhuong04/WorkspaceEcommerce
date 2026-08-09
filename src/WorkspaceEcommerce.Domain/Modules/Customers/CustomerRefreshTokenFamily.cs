using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class CustomerRefreshTokenFamily : Entity
{
    private CustomerRefreshTokenFamily()
    {
    }

    public CustomerRefreshTokenFamily(
        Guid id,
        Guid customerId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id is required for a refresh-token family.");
        }

        if (expiresAt <= createdAt)
        {
            throw new DomainException("Refresh-token family expiry must be after creation.");
        }

        CustomerId = customerId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevocationReason { get; private set; }

    public bool IsActiveAt(DateTimeOffset timestamp) => RevokedAt is null && ExpiresAt > timestamp;

    public void Revoke(DateTimeOffset revokedAt, string reason)
    {
        if (RevokedAt.HasValue)
        {
            return;
        }

        RevokedAt = revokedAt;
        RevocationReason = Guard.Required(reason, nameof(reason));
    }
}
