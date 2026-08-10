namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

using WorkspaceEcommerce.Domain.Modules.Shipments;

public interface IAppWriteStore
{
    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    void Update<TEntity>(TEntity entity)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Atomically creates one active shipment command per order/type. PostgreSQL
    /// implements this with the outbox partial unique index and ON CONFLICT so
    /// concurrent replicas do not turn a read-then-insert race into a 500.
    /// </summary>
    Task<bool> TryEnqueueShipmentCommandAsync(
        Guid orderId,
        ShipmentCommandType commandType,
        string? reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
