using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class Customer : Entity
{
    public Customer(
        Guid id,
        string fullName,
        string phoneNumber,
        string email,
        string passwordHash)
        : base(id)
    {
        FullName = Guard.Required(fullName, nameof(FullName));
        PhoneNumber = Guard.Required(phoneNumber, nameof(PhoneNumber));
        Email = NormalizeEmail(email);
        PasswordHash = Guard.Required(passwordHash, nameof(PasswordHash));
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string FullName { get; private set; }

    public string PhoneNumber { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateProfile(string fullName, string phoneNumber)
    {
        FullName = Guard.Required(fullName, nameof(FullName));
        PhoneNumber = Guard.Required(phoneNumber, nameof(PhoneNumber));
        Touch();
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.Required(passwordHash, nameof(PasswordHash));
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeEmail(string email)
    {
        return Guard.Required(email, nameof(Email)).ToLowerInvariant();
    }
}
