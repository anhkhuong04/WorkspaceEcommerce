using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

internal sealed class CustomerAccountCleanupService(
    AppDbContext dbContext,
    CustomerAccountLifecycleOptions options,
    TimeProvider timeProvider)
{
    private const string LockResource = "workspace-ecommerce:customer-account-cleanup";

    public async Task<CustomerAccountCleanupResult> CleanupAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();

        try
        {
            var cleanupLock = await PostgreSqlAdvisoryLock.TryAcquireAsync(
                dbContext,
                LockResource,
                cancellationToken);
            if (cleanupLock is null)
            {
                CustomerAccountCleanupMetrics.RecordLockUnavailable(ElapsedSince(startedAt));
                return new CustomerAccountCleanupResult(false, false, 0, 0);
            }

            await using (cleanupLock)
            {
                var now = timeProvider.GetUtcNow();
                var tokenCutoff = now.AddDays(-options.ExpiredTokenRetentionDays);
                var loginHistoryCutoff = now.AddDays(-options.LoginHistoryRetentionDays);
                var progress = new CleanupProgress();

                if (!await DrainAsync(
                    "account-token",
                    () => dbContext.CustomerAccountTokens.Where(token => token.ExpiresAt < tokenCutoff),
                    progress,
                    startedAt,
                    cancellationToken) ||
                    !await DrainAsync(
                        "refresh-token",
                        () => dbContext.CustomerRefreshTokens.Where(token => token.ExpiresAt < tokenCutoff),
                        progress,
                        startedAt,
                        cancellationToken) ||
                    !await DrainAsync(
                        "refresh-family",
                        () => dbContext.CustomerRefreshTokenFamilies.Where(family =>
                            family.ExpiresAt < tokenCutoff &&
                            !dbContext.CustomerRefreshTokens.Any(token => token.FamilyId == family.Id)),
                        progress,
                        startedAt,
                        cancellationToken) ||
                    !await DrainAsync(
                        "two-factor-challenge",
                        () => dbContext.CustomerTwoFactorChallenges.Where(challenge => challenge.ExpiresAt < tokenCutoff),
                        progress,
                        startedAt,
                        cancellationToken) ||
                    !await DrainAsync(
                        "two-factor-recovery-code",
                        () => dbContext.CustomerTwoFactorRecoveryCodes.Where(code =>
                            code.UsedAt != null && code.UsedAt < tokenCutoff),
                        progress,
                        startedAt,
                        cancellationToken) ||
                    !await DrainAsync(
                        "login-history",
                        () => dbContext.CustomerLoginHistories.Where(history =>
                            history.LoginTime < loginHistoryCutoff),
                        progress,
                        startedAt,
                        cancellationToken) ||
                    !await DrainAsync(
                        "delivered-email",
                        () => dbContext.CustomerEmailOutboxMessages.Where(message =>
                            message.SentAt != null && message.SentAt < tokenCutoff),
                        progress,
                        startedAt,
                        cancellationToken))
                {
                    CustomerAccountCleanupMetrics.RecordCompleted(ElapsedSince(startedAt), timeBudgetExhausted: true);
                    return new CustomerAccountCleanupResult(
                        true,
                        true,
                        progress.DeletedRows,
                        progress.BatchesProcessed);
                }

                CustomerAccountCleanupMetrics.RecordCompleted(ElapsedSince(startedAt), timeBudgetExhausted: false);
                return new CustomerAccountCleanupResult(
                    true,
                    false,
                    progress.DeletedRows,
                    progress.BatchesProcessed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            CustomerAccountCleanupMetrics.RecordFailure(ElapsedSince(startedAt));
            throw;
        }
    }

    private async Task<bool> DrainAsync<TEntity>(
        string dataset,
        Func<IQueryable<TEntity>> candidates,
        CleanupProgress progress,
        long cycleStartedAt,
        CancellationToken cancellationToken)
        where TEntity : Entity
    {
        while (true)
        {
            if (ElapsedSince(cycleStartedAt) >= TimeSpan.FromSeconds(options.CleanupCycleTimeSeconds))
            {
                return false;
            }

            var batch = await DeleteBatchAsync(candidates(), cancellationToken);
            if (batch.SelectedRows == 0)
            {
                return true;
            }

            progress.DeletedRows += batch.DeletedRows;
            progress.BatchesProcessed++;
            CustomerAccountCleanupMetrics.RecordDeleted(dataset, batch.DeletedRows);

            if (batch.SelectedRows < options.CleanupBatchSize)
            {
                return true;
            }
        }
    }

    private async Task<DeleteBatchResult> DeleteBatchAsync<TEntity>(
        IQueryable<TEntity> candidates,
        CancellationToken cancellationToken)
        where TEntity : Entity
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var ids = await candidates
            .OrderBy(entity => entity.Id)
            .Select(entity => entity.Id)
            .Take(options.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);
        if (ids.Length == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DeleteBatchResult(0, 0);
        }

        var deletedRows = await dbContext.Set<TEntity>()
            .Where(entity => ids.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeleteBatchResult(ids.Length, deletedRows);
    }

    private TimeSpan ElapsedSince(long startedAt) =>
        timeProvider.GetElapsedTime(startedAt);

    private sealed class CleanupProgress
    {
        public int DeletedRows { get; set; }

        public int BatchesProcessed { get; set; }
    }

    private sealed record DeleteBatchResult(int SelectedRows, int DeletedRows);
}

internal sealed record CustomerAccountCleanupResult(
    bool LockAcquired,
    bool TimeBudgetExhausted,
    int DeletedRows,
    int BatchesProcessed);
