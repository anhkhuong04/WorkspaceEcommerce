using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace WorkspaceEcommerce.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Used only by `dotnet ef` tooling — not at runtime.
/// </summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Read from environment variable first (CI/CD), then fall back to a local dev connection string
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=workspace_ecommerce_dev;Username=workspace_ecommerce;Password=hPedxEKW9iTNqu3k6laongMYLCj540FV";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(dataSourceBuilder.Build());

        return new AppDbContext(optionsBuilder.Options);
    }
}
