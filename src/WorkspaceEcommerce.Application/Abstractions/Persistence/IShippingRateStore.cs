using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IShippingRateStore
{
    IQueryable<ShippingRatePlan> Plans { get; }
    IQueryable<ShippingRateAudit> Audits { get; }
    Task<ShippingRatePlan?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<T> ExecuteConfigurationTransactionAsync<T>(Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
    void Add(ShippingRatePlan plan);
    void Add(ShippingRateAudit audit);
    void ReplaceZones(IReadOnlyCollection<ShippingRateZone> oldZones, IReadOnlyCollection<ShippingRateZone> newZones);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
