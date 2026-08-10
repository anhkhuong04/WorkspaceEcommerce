using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Modules.Operations;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

internal sealed class CustomerEmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    EmailDeliveryOptions options,
    ILogger<CustomerEmailOutboxWorker> logger) : BackgroundService
{
    private readonly string workerIdentity = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

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
        var leaseToken = Guid.NewGuid();
        var dueMessages = await dbContext.ClaimDueMessagesAsync(
            workerIdentity,
            TimeSpan.FromSeconds(options.LeaseDurationSeconds),
            leaseToken,
            options.WorkerBatchSize,
            cancellationToken);
        OutboxProcessingMetrics.RecordClaim("customer-email", dueMessages.Length);

        foreach (var message in dueMessages)
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                await deliveryService.SendAsync(payloadReader.Read(message), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordDeliveryFailureAsync(dbContext, message, leaseToken, exception, cancellationToken);
                OutboxProcessingMetrics.RecordProcessingDuration(
                    "customer-email",
                    Stopwatch.GetElapsedTime(startedAt));
                continue;
            }

            try
            {
                message.MarkSent(leaseToken, DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                OutboxProcessingMetrics.RecordCompleted("customer-email");
            }
            catch (PersistenceConcurrencyException)
            {
                DetachStaleMessage(dbContext, message);
                logger.LogWarning(
                    "Customer email outbox lease was lost before message {MessageId} could be marked delivered.",
                    message.Id);
            }
            finally
            {
                OutboxProcessingMetrics.RecordProcessingDuration(
                    "customer-email",
                    Stopwatch.GetElapsedTime(startedAt));
            }
        }
    }

    private async Task RecordDeliveryFailureAsync(
        AppDbContext dbContext,
        CustomerEmailOutboxMessage message,
        Guid leaseToken,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Exception messages can include provider request details. Persist a
        // stable category instead of letting a delivery library leak bodies.
        var error = $"Delivery failed ({exception.GetType().Name}).";
        try
        {
            if (message.AttemptCount + 1 >= options.MaxDeliveryAttempts)
            {
                message.DeadLetter(leaseToken, error, DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                OutboxProcessingMetrics.RecordDeadLetter("customer-email");
                logger.LogError(
                    "Customer email outbox message {MessageId} reached its delivery-attempt limit and was dead-lettered.",
                    message.Id);
                return;
            }

            var delay = TimeSpan.FromSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(message.AttemptCount, 6))));
            message.ScheduleRetry(leaseToken, error, DateTimeOffset.UtcNow.Add(delay));
            await dbContext.SaveChangesAsync(cancellationToken);
            OutboxProcessingMetrics.RecordRetry("customer-email");
            logger.LogWarning("Customer email delivery failed for outbox message {MessageId}; retry scheduled.", message.Id);
        }
        catch (PersistenceConcurrencyException)
        {
            DetachStaleMessage(dbContext, message);
            logger.LogWarning(
                "Customer email outbox lease was lost before a delivery failure for message {MessageId} could be recorded.",
                message.Id);
        }
    }

    private static void DetachStaleMessage(AppDbContext dbContext, CustomerEmailOutboxMessage message)
    {
        dbContext.Entry(message).State = EntityState.Detached;
    }
}
