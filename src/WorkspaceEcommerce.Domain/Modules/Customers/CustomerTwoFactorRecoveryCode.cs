using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class CustomerTwoFactorRecoveryCode : Entity
{
    public CustomerTwoFactorRecoveryCode(
        Guid id,
        Guid customerId,
        string codeHash,
        DateTimeOffset createdAt)
        : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id cannot be empty.");
        }

        CustomerId = customerId;
        CodeHash = Guard.Required(codeHash, nameof(codeHash));
        CreatedAt = createdAt;
    }

    private CustomerTwoFactorRecoveryCode()
    {
    }

    public Guid CustomerId { get; private set; }

    public string CodeHash { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsUnused => !UsedAt.HasValue;

    public void MarkUsed(DateTimeOffset usedAt)
    {
        if (UsedAt.HasValue)
        {
            throw new DomainException("Recovery code has already been used.");
        }

        UsedAt = usedAt;
    }
}
