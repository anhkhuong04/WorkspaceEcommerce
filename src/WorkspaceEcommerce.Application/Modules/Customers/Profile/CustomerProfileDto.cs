namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

public sealed record CustomerProfileDto(
    Guid Id,
    string FullName,
    string? PhoneNumber,
    string Email,
    string? AvatarUrl,
    bool IsEmailVerified,
    int RewardPoints,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
