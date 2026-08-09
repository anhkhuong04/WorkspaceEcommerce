using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using WorkspaceEcommerce.Application.Abstractions.Authentication;

namespace WorkspaceEcommerce.Infrastructure.Configuration;

public static class AuthenticationConfigurationValidator
{
    private const int MinimumSigningKeyBytes = 32;

    internal static AdminAuthOptions GetValidatedAdminAuthOptions(this IConfiguration configuration)
    {
        var options = new AdminAuthOptions
        {
            Email = configuration[$"{AdminAuthOptions.SectionName}:{nameof(AdminAuthOptions.Email)}"] ?? string.Empty,
            Password = configuration[$"{AdminAuthOptions.SectionName}:{nameof(AdminAuthOptions.Password)}"] ?? string.Empty
        };

        ValidateRequiredSecret(AdminAuthOptions.SectionName, nameof(AdminAuthOptions.Email), options.Email);
        ValidateRequiredSecret(AdminAuthOptions.SectionName, nameof(AdminAuthOptions.Password), options.Password);

        try
        {
            _ = new MailAddress(options.Email);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"Configuration '{AdminAuthOptions.SectionName}:{nameof(AdminAuthOptions.Email)}' must be a valid email address.",
                exception);
        }

        return options;
    }

    public static JwtOptions GetValidatedJwtOptions(this IConfiguration configuration)
    {
        var accessTokenMinutesValue = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.AccessTokenMinutes)}"];
        _ = int.TryParse(accessTokenMinutesValue, out var accessTokenMinutes);

        var options = new JwtOptions
        {
            Issuer = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)}"] ?? string.Empty,
            Audience = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)}"] ?? string.Empty,
            SigningKey = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"] ?? string.Empty,
            AccessTokenMinutes = accessTokenMinutes
        };

        ValidateRequiredSecret(JwtOptions.SectionName, nameof(JwtOptions.Issuer), options.Issuer);
        ValidateRequiredSecret(JwtOptions.SectionName, nameof(JwtOptions.Audience), options.Audience);
        ValidateRequiredSecret(JwtOptions.SectionName, nameof(JwtOptions.SigningKey), options.SigningKey);

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Configuration '{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' must be at least {MinimumSigningKeyBytes} bytes for HS256.");
        }

        if (options.AccessTokenMinutes is <= 0 or > 1440)
        {
            throw new InvalidOperationException(
                $"Configuration '{JwtOptions.SectionName}:{nameof(JwtOptions.AccessTokenMinutes)}' must be between 1 and 1440 minutes.");
        }

        return options;
    }

    public static GoogleAuthOptions GetValidatedGoogleAuthOptions(this IConfiguration configuration)
    {
        var enabledValue = configuration[$"{GoogleAuthOptions.SectionName}:{nameof(GoogleAuthOptions.Enabled)}"];
        if (!string.IsNullOrWhiteSpace(enabledValue) && !bool.TryParse(enabledValue, out _))
        {
            throw new InvalidOperationException(
                $"Configuration '{GoogleAuthOptions.SectionName}:{nameof(GoogleAuthOptions.Enabled)}' must be true or false.");
        }

        var enabled = bool.TryParse(enabledValue, out var configuredEnabled) && configuredEnabled;

        var allowedClientIds = configuration
            .GetSection($"{GoogleAuthOptions.SectionName}:{nameof(GoogleAuthOptions.AllowedClientIds)}")
            .GetChildren()
            .Select(section => section.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (enabled && allowedClientIds.Length == 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{GoogleAuthOptions.SectionName}:{nameof(GoogleAuthOptions.AllowedClientIds)}' must contain at least one Google OAuth client ID when Google login is enabled.");
        }

        if (enabled && allowedClientIds.Any(ConfigurationPlaceholders.ContainsPlaceholder))
        {
            throw new InvalidOperationException(
                $"Configuration '{GoogleAuthOptions.SectionName}:{nameof(GoogleAuthOptions.AllowedClientIds)}' contains a placeholder value. Configure server-owned Google OAuth client IDs before enabling Google login.");
        }

        return new GoogleAuthOptions
        {
            Enabled = enabled,
            AllowedClientIds = allowedClientIds
        };
    }

    public static TwoFactorOptions GetValidatedTwoFactorOptions(this IConfiguration configuration)
    {
        var section = configuration.GetSection(TwoFactorOptions.SectionName);
        var options = section.Get<TwoFactorOptions>() ?? new TwoFactorOptions();

        if (string.IsNullOrWhiteSpace(options.Issuer) || ConfigurationPlaceholders.ContainsPlaceholder(options.Issuer))
        {
            throw new InvalidOperationException(
                $"Configuration '{TwoFactorOptions.SectionName}:{nameof(TwoFactorOptions.Issuer)}' must be a non-placeholder issuer.");
        }

        if (options.SetupLifetimeMinutes is < 1 or > 30)
        {
            throw new InvalidOperationException(
                $"Configuration '{TwoFactorOptions.SectionName}:{nameof(TwoFactorOptions.SetupLifetimeMinutes)}' must be between 1 and 30 minutes.");
        }

        if (options.ChallengeLifetimeMinutes is < 1 or > 15)
        {
            throw new InvalidOperationException(
                $"Configuration '{TwoFactorOptions.SectionName}:{nameof(TwoFactorOptions.ChallengeLifetimeMinutes)}' must be between 1 and 15 minutes.");
        }

        if (options.RecoveryCodeCount is < 5 or > 20)
        {
            throw new InvalidOperationException(
                $"Configuration '{TwoFactorOptions.SectionName}:{nameof(TwoFactorOptions.RecoveryCodeCount)}' must be between 5 and 20.");
        }

        return new TwoFactorOptions
        {
            Issuer = options.Issuer.Trim(),
            SetupLifetimeMinutes = options.SetupLifetimeMinutes,
            ChallengeLifetimeMinutes = options.ChallengeLifetimeMinutes,
            RecoveryCodeCount = options.RecoveryCodeCount
        };
    }

    private static void ValidateRequiredSecret(string sectionName, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration '{sectionName}:{key}' is required.");
        }

        if (ConfigurationPlaceholders.ContainsPlaceholder(value))
        {
            throw new InvalidOperationException(
                $"Configuration '{sectionName}:{key}' contains a placeholder value. Configure it with user secrets, environment variables, or a local untracked settings file.");
        }
    }
}
