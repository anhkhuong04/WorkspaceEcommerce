namespace WorkspaceEcommerce.Api.Configuration;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int WindowSeconds { get; init; } = 60;

    public int AuthPermitLimit { get; init; } = 10;

    public int BlogCommentPermitLimit { get; init; } = 3;

    public int TwoFactorVerificationPermitLimit { get; init; } = 5;

    public int TwoFactorSetupPermitLimit { get; init; } = 5;

    public int CheckoutPermitLimit { get; init; } = 60;

    public int PaymentPermitLimit { get; init; } = 60;

    public int ProviderWebhookPermitLimit { get; init; } = 120;

    public int CatalogPermitLimit { get; init; } = 240;

    public int WarrantyLookupPermitLimit { get; init; } = 15;

    public int WarrantyActivationPermitLimit { get; init; } = 8;

    public int GuestReceiptPermitLimit { get; init; } = 10;

    public int DefaultPermitLimit { get; init; } = 120;

    public void Validate()
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [nameof(WindowSeconds)] = WindowSeconds,
            [nameof(AuthPermitLimit)] = AuthPermitLimit,
            [nameof(BlogCommentPermitLimit)] = BlogCommentPermitLimit,
            [nameof(TwoFactorVerificationPermitLimit)] = TwoFactorVerificationPermitLimit,
            [nameof(TwoFactorSetupPermitLimit)] = TwoFactorSetupPermitLimit,
            [nameof(CheckoutPermitLimit)] = CheckoutPermitLimit,
            [nameof(PaymentPermitLimit)] = PaymentPermitLimit,
            [nameof(ProviderWebhookPermitLimit)] = ProviderWebhookPermitLimit,
            [nameof(CatalogPermitLimit)] = CatalogPermitLimit,
            [nameof(WarrantyLookupPermitLimit)] = WarrantyLookupPermitLimit,
            [nameof(WarrantyActivationPermitLimit)] = WarrantyActivationPermitLimit,
            [nameof(GuestReceiptPermitLimit)] = GuestReceiptPermitLimit,
            [nameof(DefaultPermitLimit)] = DefaultPermitLimit
        };

        foreach (var (name, value) in values)
        {
            if (value <= 0)
            {
                throw new InvalidOperationException(
                    $"Configuration '{SectionName}:{name}' must be greater than zero.");
            }
        }
    }
}
