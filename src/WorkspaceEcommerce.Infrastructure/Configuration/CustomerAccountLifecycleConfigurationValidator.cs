using WorkspaceEcommerce.Application.Abstractions.Authentication;
using Microsoft.Extensions.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Configuration;

public static class CustomerAccountLifecycleConfigurationValidator
{
    public static CustomerAccountLifecycleOptions GetValidatedCustomerAccountLifecycleOptions(
        this IConfiguration configuration)
    {
        var configured = configuration.GetSection(CustomerAccountLifecycleOptions.SectionName)
            .Get<CustomerAccountLifecycleOptions>() ?? new CustomerAccountLifecycleOptions();
        var storefrontBaseUrl = configuration["Storefront:BaseUrl"]?.Trim() ?? string.Empty;

        if (!Uri.TryCreate(storefrontBaseUrl, UriKind.Absolute, out var storefrontUri) ||
            storefrontUri.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("Configuration 'Storefront:BaseUrl' must be an absolute http(s) URL.");
        }

        if (configured.EmailVerificationLifetimeMinutes is < 5 or > 10080 ||
            configured.PasswordResetLifetimeMinutes is < 5 or > 1440 ||
            configured.RefreshTokenLifetimeDays is < 1 or > 90 ||
            configured.CleanupIntervalHours is < 1 or > 168 ||
            configured.ExpiredTokenRetentionDays is < 1 or > 365 ||
            configured.LoginHistoryRetentionDays is < 30 or > 3650)
        {
            throw new InvalidOperationException(
                $"Configuration '{CustomerAccountLifecycleOptions.SectionName}' has a value outside the supported security retention range.");
        }

        return new CustomerAccountLifecycleOptions
        {
            EmailVerificationLifetimeMinutes = configured.EmailVerificationLifetimeMinutes,
            PasswordResetLifetimeMinutes = configured.PasswordResetLifetimeMinutes,
            RefreshTokenLifetimeDays = configured.RefreshTokenLifetimeDays,
            StorefrontBaseUrl = storefrontUri.GetLeftPart(UriPartial.Authority) + storefrontUri.AbsolutePath.TrimEnd('/'),
            CleanupIntervalHours = configured.CleanupIntervalHours,
            ExpiredTokenRetentionDays = configured.ExpiredTokenRetentionDays,
            LoginHistoryRetentionDays = configured.LoginHistoryRetentionDays
        };
    }
}
