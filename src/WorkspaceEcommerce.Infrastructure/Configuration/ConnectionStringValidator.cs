using Microsoft.Extensions.Configuration;
using Npgsql;

namespace WorkspaceEcommerce.Infrastructure.Configuration;

internal static class ConnectionStringValidator
{
    private const string DefaultConnectionName = "DefaultConnection";

    public static string GetValidatedDefaultConnectionString(this IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DefaultConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{DefaultConnectionName}' is required.");
        }

        if (ConfigurationPlaceholders.ContainsPlaceholder(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{DefaultConnectionName}' contains a placeholder value. Configure it with user secrets, environment variables, or a local untracked settings file.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or FormatException)
        {
            throw new InvalidOperationException($"Connection string '{DefaultConnectionName}' is invalid.", exception);
        }

        if (string.IsNullOrWhiteSpace(builder.Host))
        {
            throw new InvalidOperationException($"Connection string '{DefaultConnectionName}' must include Host.");
        }

        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException($"Connection string '{DefaultConnectionName}' must include Database.");
        }

        return connectionString;
    }
}
