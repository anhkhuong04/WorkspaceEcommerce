using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace WorkspaceEcommerce.Infrastructure.Persistence;

/// <summary>
/// Holds a PostgreSQL session advisory lock on the same connection used by an
/// <see cref="AppDbContext"/>. This gives periodic cleanup work one elected
/// leader across API replicas without adding another coordinator dependency.
/// </summary>
internal sealed class PostgreSqlAdvisoryLock : IAsyncDisposable
{
    private readonly DbConnection connection;
    private readonly string resource;
    private readonly bool closeConnectionOnRelease;
    private bool released;

    private PostgreSqlAdvisoryLock(
        DbConnection connection,
        string resource,
        bool closeConnectionOnRelease)
    {
        this.connection = connection;
        this.resource = resource;
        this.closeConnectionOnRelease = closeConnectionOnRelease;
    }

    public static async Task<PostgreSqlAdvisoryLock?> TryAcquireAsync(
        AppDbContext dbContext,
        string resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("An advisory-lock resource name is required.", nameof(resource));
        }

        var connection = dbContext.Database.GetDbConnection();
        var closeConnectionOnRelease = connection.State != ConnectionState.Open;
        if (closeConnectionOnRelease)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(hashtext(@resource));";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "resource";
            parameter.Value = resource;
            command.Parameters.Add(parameter);

            var acquired = await command.ExecuteScalarAsync(cancellationToken) is true;
            if (!acquired)
            {
                if (closeConnectionOnRelease)
                {
                    await connection.CloseAsync();
                }

                return null;
            }

            return new PostgreSqlAdvisoryLock(connection, resource, closeConnectionOnRelease);
        }
        catch
        {
            if (closeConnectionOnRelease)
            {
                await connection.CloseAsync();
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (released)
        {
            return;
        }

        released = true;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(hashtext(@resource));";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "resource";
            parameter.Value = resource;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync();
        }
        finally
        {
            if (closeConnectionOnRelease)
            {
                await connection.CloseAsync();
            }
        }
    }
}
