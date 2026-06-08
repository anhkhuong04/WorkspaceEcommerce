namespace WorkspaceEcommerce.Application.Abstractions.Seeding;

public interface IDemoDataSeeder
{
    Task<DemoDataSeedResult> SeedAsync(CancellationToken cancellationToken = default);
}
