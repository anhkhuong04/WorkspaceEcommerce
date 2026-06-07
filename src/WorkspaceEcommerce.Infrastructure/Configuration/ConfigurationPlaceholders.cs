namespace WorkspaceEcommerce.Infrastructure.Configuration;

internal static class ConfigurationPlaceholders
{
    public static bool ContainsPlaceholder(string value)
    {
        return value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            || value.Contains("<", StringComparison.Ordinal)
            || value.Contains(">", StringComparison.Ordinal);
    }
}
