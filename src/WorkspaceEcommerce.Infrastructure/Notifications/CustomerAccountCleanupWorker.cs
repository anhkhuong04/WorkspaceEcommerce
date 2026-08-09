using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

internal sealed class CustomerAccountCleanupWorker(
    IServiceScopeFactory scopeFactory,
    CustomerAccountLifecycleOptions options,
    ILogger<CustomerAccountCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(options.CleanupIntervalHours);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Customer account cleanup worker iteration failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var tokenCutoff = now.AddDays(-options.ExpiredTokenRetentionDays);
        var loginHistoryCutoff = now.AddDays(-options.LoginHistoryRetentionDays);

        var accountTokens = await dbContext.CustomerAccountTokens
            .Where(token => token.ExpiresAt < tokenCutoff)
            .ToArrayAsync(cancellationToken);
        var refreshTokens = await dbContext.CustomerRefreshTokens
            .Where(token => token.ExpiresAt < tokenCutoff)
            .ToArrayAsync(cancellationToken);
        var refreshFamilies = await dbContext.CustomerRefreshTokenFamilies
            .Where(family => family.ExpiresAt < tokenCutoff)
            .ToArrayAsync(cancellationToken);
        var challenges = await dbContext.CustomerTwoFactorChallenges
            .Where(challenge => challenge.ExpiresAt < tokenCutoff)
            .ToArrayAsync(cancellationToken);
        var recoveryCodes = await dbContext.CustomerTwoFactorRecoveryCodes
            .Where(code => code.UsedAt != null && code.UsedAt < tokenCutoff)
            .ToArrayAsync(cancellationToken);
        var loginHistory = await dbContext.CustomerLoginHistories
            .Where(history => history.LoginTime < loginHistoryCutoff)
            .ToArrayAsync(cancellationToken);
        var deliveredEmails = await dbContext.CustomerEmailOutboxMessages
            .Where(message => message.SentAt != null && message.SentAt < tokenCutoff)
            .ToArrayAsync(cancellationToken);

        dbContext.RemoveRange(accountTokens);
        dbContext.RemoveRange(refreshTokens);
        dbContext.RemoveRange(refreshFamilies);
        dbContext.RemoveRange(challenges);
        dbContext.RemoveRange(recoveryCodes);
        dbContext.RemoveRange(loginHistory);
        dbContext.RemoveRange(deliveredEmails);
        if (accountTokens.Length + refreshTokens.Length + refreshFamilies.Length + challenges.Length +
            recoveryCodes.Length + loginHistory.Length + deliveredEmails.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
