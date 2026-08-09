namespace WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;

public sealed record TwoFactorSetupStartResponse(
    string ManualEntryKey,
    string ProvisioningUri,
    DateTimeOffset ExpiresAt);

public sealed record ConfirmTwoFactorSetupRequest(string Code);

public sealed record TwoFactorSetupConfirmationResponse(IReadOnlyList<string> RecoveryCodes);

public sealed record DisableTwoFactorRequest(string? Code, string? RecoveryCode);

public sealed record VerifyTwoFactorLoginRequest(string ChallengeToken, string Code);

public sealed record VerifyTwoFactorRecoveryRequest(string ChallengeToken, string RecoveryCode);
