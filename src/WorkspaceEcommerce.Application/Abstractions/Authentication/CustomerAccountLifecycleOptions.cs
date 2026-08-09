namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

public sealed class CustomerAccountLifecycleOptions
{
    public const string SectionName = "CustomerAccountLifecycle";

    public int EmailVerificationLifetimeMinutes { get; init; } = 1440;

    public int PasswordResetLifetimeMinutes { get; init; } = 30;

    public int RefreshTokenLifetimeDays { get; init; } = 30;

    public string StorefrontBaseUrl { get; init; } = "http://localhost:5173";

    public int CleanupIntervalHours { get; init; } = 24;

    public int ExpiredTokenRetentionDays { get; init; } = 7;

    public int LoginHistoryRetentionDays { get; init; } = 90;
}
