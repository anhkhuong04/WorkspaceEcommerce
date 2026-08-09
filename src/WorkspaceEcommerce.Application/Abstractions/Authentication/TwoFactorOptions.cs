namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

public sealed class TwoFactorOptions
{
    public const string SectionName = "TwoFactor";

    public string Issuer { get; init; } = "WorkspaceEcommerce";

    public int SetupLifetimeMinutes { get; init; } = 10;

    public int ChallengeLifetimeMinutes { get; init; } = 5;

    public int RecoveryCodeCount { get; init; } = 10;
}
