using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace WorkspaceEcommerce.Application.Common.Persistence;

internal static class QueryableAsyncExtensions
{
    public static Task<T[]> ToArrayAsyncSafe<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IsEfAsyncQuery(query)
            ? query.ToArrayAsync(cancellationToken)
            : Task.FromResult(query.ToArray());
    }

    public static Task<int> CountAsyncSafe<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IsEfAsyncQuery(query)
            ? query.CountAsync(cancellationToken)
            : Task.FromResult(query.Count());
    }

    public static Task<bool> AnyAsyncSafe<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IsEfAsyncQuery(query)
            ? query.AnyAsync(cancellationToken)
            : Task.FromResult(query.Any());
    }

    public static Task<T?> FirstOrDefaultAsyncSafe<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IsEfAsyncQuery(query)
            ? query.FirstOrDefaultAsync(cancellationToken)
            : Task.FromResult(query.FirstOrDefault());
    }

    public static Task<Dictionary<TKey, TSource>> ToDictionaryAsyncSafe<TSource, TKey>(
        this IQueryable<TSource> query,
        Func<TSource, TKey> keySelector,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IsEfAsyncQuery(query)
            ? query.ToDictionaryAsync(keySelector, cancellationToken)
            : Task.FromResult(query.ToDictionary(keySelector));
    }

    private static bool IsEfAsyncQuery<T>(IQueryable<T> query)
    {
        return query.Provider is IAsyncQueryProvider;
    }
}
