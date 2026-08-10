using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

/// <summary>
/// PostgreSQL-specific claiming for the durable customer email outbox.
/// The transaction is deliberately limited to selecting and leasing rows; no
/// network delivery occurs while database row locks are held.
/// </summary>
internal static class CustomerEmailOutboxLeaseStore
{
    internal static async Task<CustomerEmailOutboxMessage[]> ClaimDueMessagesAsync(
        this AppDbContext dbContext,
        string leaseOwner,
        TimeSpan leaseDuration,
        Guid leaseToken,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new ArgumentException("An email outbox lease owner is required.", nameof(leaseOwner));
        }

        if (leaseToken == Guid.Empty)
        {
            throw new ArgumentException("A non-empty email outbox lease token is required.", nameof(leaseToken));
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The lease duration must be positive.");
        }

        if (batchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "The email outbox batch size must be between 1 and 100.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var databaseNow = await dbContext.Database
            .SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        var leaseExpiresAt = databaseNow.Add(leaseDuration);
        var dueMessages = await dbContext.CustomerEmailOutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM customer.email_outbox
                WHERE status IN ('Pending', 'Leased')
                  AND sent_at IS NULL
                  AND dead_lettered_at IS NULL
                  AND next_attempt_at <= clock_timestamp()
                  AND (lease_expires_at IS NULL OR lease_expires_at <= clock_timestamp())
                ORDER BY next_attempt_at, id
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToArrayAsync(cancellationToken);

        foreach (var message in dueMessages)
        {
            message.Claim(leaseOwner, leaseToken, databaseNow, leaseExpiresAt);
        }

        if (dueMessages.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return dueMessages;
    }
}
