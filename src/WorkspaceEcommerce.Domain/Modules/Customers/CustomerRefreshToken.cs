using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class CustomerRefreshToken : Entity
{
    private CustomerRefreshToken()
    {
    }

    public CustomerRefreshToken(
        Guid id,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        if (familyId == Guid.Empty)
        {
            throw new DomainException("Refresh-token family id is required.");
        }

        if (expiresAt <= createdAt)
        {
            throw new DomainException("Refresh-token expiry must be after creation.");
        }

        FamilyId = familyId;
        TokenHash = Guard.Required(tokenHash, nameof(tokenHash));
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsUsableAt(DateTimeOffset timestamp) => UsedAt is null && ExpiresAt > timestamp;

    public void MarkUsed(DateTimeOffset usedAt)
    {
        if (UsedAt.HasValue)
        {
            throw new DomainException("A refresh token may only be used once.");
        }

        UsedAt = usedAt;
    }
}
