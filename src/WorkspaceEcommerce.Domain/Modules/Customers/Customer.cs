using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class Customer : Entity
{
    // Single canonical constructor (private, all optional nullable)
    private Customer(
        Guid id,
        string fullName,
        string? phoneNumber,
        string email,
        string? passwordHash,
        string? googleId,
        string? avatarUrl,
        bool isEmailVerified)
        : base(id)
    {
        FullName = Guard.Required(fullName, nameof(FullName));
        PhoneNumber = phoneNumber;
        Email = NormalizeEmail(email);
        PasswordHash = passwordHash;
        GoogleId = googleId;
        AvatarUrl = avatarUrl;
        IsEmailVerified = isEmailVerified;
        RewardPoints = 0;
        TwoFactorEnabled = false;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    // EF Core parameterless constructor
    private Customer() { }

    public string FullName { get; private set; } = default!;

    public string? PhoneNumber { get; private set; }

    public string Email { get; private set; } = default!;

    public string? PasswordHash { get; private set; }

    public string? GoogleId { get; private set; }

    public string? AvatarUrl { get; private set; }

    public bool IsEmailVerified { get; private set; }

    public int RewardPoints { get; private set; }

    public bool TwoFactorEnabled { get; private set; }

    public string? TwoFactorSecret { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates a customer registered via email and password.</summary>
    public static Customer Create(
        Guid id,
        string fullName,
        string phoneNumber,
        string email,
        string passwordHash)
    {
        return new Customer(
            id,
            Guard.Required(fullName, nameof(FullName)),
            Guard.Required(phoneNumber, nameof(PhoneNumber)),
            email,
            Guard.Required(passwordHash, nameof(PasswordHash)),
            googleId: null,
            avatarUrl: null,
            isEmailVerified: false);
    }

    /// <summary>Creates a customer registered via Google OAuth (no password, no phone).</summary>
    public static Customer CreateFromGoogle(
        Guid id,
        string fullName,
        string email,
        string googleId,
        string? avatarUrl = null)
    {
        return new Customer(
            id,
            Guard.Required(fullName, nameof(FullName)),
            phoneNumber: null,
            email,
            passwordHash: null,
            Guard.Required(googleId, nameof(GoogleId)),
            avatarUrl,
            isEmailVerified: true);
    }

    public void LinkGoogleAccount(string googleId)
    {
        GoogleId = Guard.Required(googleId, nameof(GoogleId));
        IsEmailVerified = true;
        Touch();
    }

    public void MarkEmailVerified()
    {
        IsEmailVerified = true;
        Touch();
    }

    public void UpdateProfile(string fullName, string phoneNumber)
    {
        FullName = Guard.Required(fullName, nameof(FullName));
        PhoneNumber = Guard.Required(phoneNumber, nameof(PhoneNumber));
        Touch();
    }

    public void UpdateAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl?.Trim() is { Length: > 0 } url ? url : null;
        Touch();
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.Required(passwordHash, nameof(PasswordHash));
        Touch();
    }

    public void EnableTwoFactor(string secret)
    {
        TwoFactorSecret = Guard.Required(secret, nameof(TwoFactorSecret));
        TwoFactorEnabled = true;
        Touch();
    }

    public void DisableTwoFactor()
    {
        TwoFactorSecret = null;
        TwoFactorEnabled = false;
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
