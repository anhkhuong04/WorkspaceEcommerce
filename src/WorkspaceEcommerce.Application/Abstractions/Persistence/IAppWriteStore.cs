namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IAppWriteStore
{
    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    void Update<TEntity>(TEntity entity)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
