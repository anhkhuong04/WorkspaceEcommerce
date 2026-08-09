using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Persistence;

/// <summary>
/// Creates the EF Core context without embedding credentials in source control.
/// </summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__DefaultConnection";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        string connectionString;

        try
        {
            connectionString = configuration.GetValidatedDefaultConnectionString();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"EF design-time configuration requires '{ConnectionStringEnvironmentVariable}' or an untracked " +
                "src/WorkspaceEcommerce.Api/appsettings.Local.json. Copy appsettings.Local.example.json " +
                "and configure a non-placeholder connection string.",
                exception);
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(dataSourceBuilder.Build());

        return new AppDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var apiProjectDirectory = FindApiProjectDirectory();
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        return new ConfigurationBuilder()
            .SetBasePath(apiProjectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string FindApiProjectDirectory()
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            workingDirectory,
            Path.Combine(workingDirectory, "src", "WorkspaceEcommerce.Api"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        };

        var apiProjectDirectory = candidates.FirstOrDefault(candidate =>
            File.Exists(Path.Combine(candidate, "appsettings.json")));

        return apiProjectDirectory ?? throw new InvalidOperationException(
            "Could not find the API configuration directory for EF design-time tooling. " +
            "Run the command from the repository root or the API project directory.");
    }
}
