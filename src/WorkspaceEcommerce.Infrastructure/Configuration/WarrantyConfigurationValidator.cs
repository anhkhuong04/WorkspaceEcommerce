using WorkspaceEcommerce.Application.Abstractions.Warranties;
using Microsoft.Extensions.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Configuration;

public static class WarrantyConfigurationValidator
{
    public static WarrantyOptions GetValidatedWarrantyOptions(this IConfiguration configuration)
    {
        var configured = configuration.GetSection(WarrantyOptions.SectionName)
            .Get<WarrantyOptions>() ?? new WarrantyOptions();

        if (configured.IdentifierKeyVersion < 1 || configured.IdentifierKeyVersion > 100 ||
            configured.MaxImportRows is < 1 or > 10_000)
        {
            throw new InvalidOperationException(
                $"Configuration '{WarrantyOptions.SectionName}' contains unsupported limits.");
        }

        if (configured.IdentifierHmacKeys.Any(pair => pair.Key is < 1 or > 100 ||
                string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Trim().Length < 32 || IsPlaceholder(pair.Value)))
        {
            throw new InvalidOperationException(
                "Configuration 'Warranty:IdentifierHmacKeys' contains an invalid key version or secret.");
        }

        var anyFeatureEnabled = configured.Enabled || configured.AdminEnabled ||
            configured.ActivationEnabled || configured.PublicLookupEnabled;
        if (anyFeatureEnabled)
        {
            var hasCurrentKey = configured.IdentifierHmacKeys.TryGetValue(configured.IdentifierKeyVersion, out var versionedCurrentKey) &&
                !string.IsNullOrWhiteSpace(versionedCurrentKey);
            if (!hasCurrentKey && (string.IsNullOrWhiteSpace(configured.IdentifierHmacKey) ||
                configured.IdentifierHmacKey.Trim().Length < 32 ||
                IsPlaceholder(configured.IdentifierHmacKey)))
            {
                throw new InvalidOperationException(
                    "Configuration 'Warranty:IdentifierHmacKey' must be a non-placeholder secret of at least 32 characters whenever the warranty module is enabled.");
            }
        }

        return configured;
    }

    private static bool IsPlaceholder(string value) =>
        value.Contains("CHANGE", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("${", StringComparison.Ordinal);
}
