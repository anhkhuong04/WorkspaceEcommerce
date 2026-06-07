namespace WorkspaceEcommerce.Domain.Common;

internal static class Guard
{
    public static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        return value.Trim();
    }

    public static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static decimal NotNegative(decimal value, string name)
    {
        if (value < 0)
        {
            throw new DomainException($"{name} cannot be negative.");
        }

        return value;
    }

    public static int NotNegative(int value, string name)
    {
        if (value < 0)
        {
            throw new DomainException($"{name} cannot be negative.");
        }

        return value;
    }
}
