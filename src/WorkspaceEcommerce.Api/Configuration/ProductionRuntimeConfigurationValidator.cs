using System.Net;

namespace WorkspaceEcommerce.Api.Configuration;

/// <summary>
/// Fails closed for production-only settings that ASP.NET Core would otherwise
/// accept with an unsafe default. Provider credentials are validated by the
/// Infrastructure configuration validators.
/// </summary>
public static class ProductionRuntimeConfigurationValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsDevelopment())
        {
            return;
        }

        ValidateAllowedHosts(configuration["AllowedHosts"]);
        ValidateExternalKeyRing(configuration["DataProtection:KeyRingPath"]);
        ValidateRequiredValue(
            "APPLICATIONINSIGHTS_CONNECTION_STRING (or ApplicationInsights:ConnectionString)",
            configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                ?? configuration["ApplicationInsights:ConnectionString"]);
        ValidateHttpsUrl("Storefront:BaseUrl", configuration["Storefront:BaseUrl"]);
    }

    private static void ValidateAllowedHosts(string? rawHosts)
    {
        var hosts = (rawHosts ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (hosts.Length == 0)
        {
            throw new InvalidOperationException(
                "Configuration 'AllowedHosts' must list exact host names outside Development.");
        }

        foreach (var host in hosts)
        {
            if (host.Contains('*', StringComparison.Ordinal) ||
                host.Contains('/', StringComparison.Ordinal) ||
                host.Contains("//", StringComparison.Ordinal) ||
                string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Configuration 'AllowedHosts' must not contain wildcard, URL, or localhost values outside Development.");
            }

            var normalizedHost = host.Trim('[', ']');
            if (IPAddress.TryParse(normalizedHost, out _))
            {
                continue;
            }

            if (host.Contains(':', StringComparison.Ordinal) ||
                Uri.CheckHostName(host) is UriHostNameType.Unknown)
            {
                throw new InvalidOperationException(
                    $"Configuration 'AllowedHosts' contains invalid host '{host}'. Use host names without scheme, path, or port.");
            }
        }
    }

    private static void ValidateExternalKeyRing(string? keyRingPath)
    {
        ValidateRequiredValue("DataProtection:KeyRingPath", keyRingPath);

        if (!Path.IsPathFullyQualified(keyRingPath!))
        {
            throw new InvalidOperationException(
                "Configuration 'DataProtection:KeyRingPath' must be an absolute path to a persistent, access-controlled shared mount outside Development.");
        }
    }

    private static void ValidateHttpsUrl(string key, string? value)
    {
        ValidateRequiredValue(key, value);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Configuration '{key}' must be an absolute HTTPS URL outside Development.");
        }
    }

    private static void ValidateRequiredValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must come from the external production configuration authority and cannot be empty or a placeholder.");
        }
    }

    private static bool IsPlaceholder(string value)
    {
        var normalized = value.Trim();
        return normalized.Contains("CHANGE", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("${", StringComparison.Ordinal) ||
            (normalized.StartsWith('<') && normalized.EndsWith('>'));
    }
}
