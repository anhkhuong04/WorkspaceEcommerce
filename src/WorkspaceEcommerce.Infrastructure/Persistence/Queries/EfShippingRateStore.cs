using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Queries;

internal sealed class EfShippingRateStore(AppDbContext context) : IShippingRateStore
{
    public IQueryable<ShippingRatePlan> Plans => context.Set<ShippingRatePlan>();
    public IQueryable<ShippingRateAudit> Audits => context.Set<ShippingRateAudit>();

    public Task<ShippingRatePlan?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        // One statement gives revision and zone membership the same PostgreSQL
        // snapshot, including when an administrator previews a concurrently edited draft.
        // This is one nested collection chain, bounded by the province catalog.
        context.Set<ShippingRatePlan>().Include(plan => plan.Zones).ThenInclude(zone => zone.Provinces)
            .AsSingleQuery().SingleOrDefaultAsync(plan => plan.Id == id, cancellationToken);

    public async Task<T> ExecuteConfigurationTransactionAsync<T>(Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // One module-specific transaction lock serializes configuration writes on every replica.
        // The unique Published index and revision tokens additionally protect database invariants.
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({782314006L})", cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public void Add(ShippingRatePlan plan) => context.Add(plan);
    public void Add(ShippingRateAudit audit) => context.Add(audit);
    public void ReplaceZones(IReadOnlyCollection<ShippingRateZone> oldZones, IReadOnlyCollection<ShippingRateZone> newZones)
    {
        context.RemoveRange(oldZones);
        context.AddRange(newZones);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new PersistenceConcurrencyException("Shipping configuration conflicts with persisted data.", exception);
        }
    }
}
