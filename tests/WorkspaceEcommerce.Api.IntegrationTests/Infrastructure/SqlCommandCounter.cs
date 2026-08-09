using System.Data.Common;
using System.Threading;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

internal sealed class SqlCommandCounter : DbCommandInterceptor
{
    private int _selectCount;

    public int SelectCount => Volatile.Read(ref _selectCount);

    public void Reset() => Interlocked.Exchange(ref _selectCount, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        CountSelect(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        CountSelect(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        CountSelect(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        CountSelect(command);
        return ValueTask.FromResult(result);
    }

    private void CountSelect(DbCommand command)
    {
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _selectCount);
        }
    }
}
