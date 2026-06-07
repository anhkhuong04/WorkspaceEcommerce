using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;

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
