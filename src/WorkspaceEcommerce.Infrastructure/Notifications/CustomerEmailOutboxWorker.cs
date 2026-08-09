using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

internal sealed class CustomerEmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    EmailDeliveryOptions options,
    ILogger<CustomerEmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.WorkerIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Customer email outbox worker iteration failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ProcessDueMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payloadReader = scope.ServiceProvider.GetRequiredService<CustomerEmailOutboxPayloadReader>();
        var deliveryService = scope.ServiceProvider.GetRequiredService<ICustomerEmailDeliveryService>();
        var now = DateTimeOffset.UtcNow;
        var dueMessages = await dbContext.CustomerEmailOutboxMessages
            .Where(message => message.SentAt == null && message.NextAttemptAt <= now)
            .OrderBy(message => message.NextAttemptAt)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        foreach (var message in dueMessages)
        {
            try
            {
                await deliveryService.SendAsync(payloadReader.Read(message), cancellationToken);
                message.MarkSent(DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(message.AttemptCount, 6))));
                // Exception messages can include provider request details. Persist a
                // stable category instead of letting a delivery library leak bodies.
                message.ScheduleRetry($"Delivery failed ({exception.GetType().Name}).", DateTimeOffset.UtcNow.Add(delay));
                logger.LogWarning("Customer email delivery failed for outbox message {MessageId}; retry scheduled.", message.Id);
            }
        }

        if (dueMessages.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
