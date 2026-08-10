using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Modules.Operations;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

/// <summary>
/// Refreshes queue gauges without coupling domain workers to a telemetry
/// exporter. Every replica reports the same bounded database snapshot; alert
/// queries should aggregate it by maximum rather than sum across replicas.
/// </summary>
internal sealed class OutboxMetricsWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxMetricsWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var operations = scope.ServiceProvider.GetRequiredService<IOutboxOperationsService>();
                var result = await operations.GetSummaryAsync(stoppingToken);
                if (result.IsSuccess && result.Value is not null)
                {
                    foreach (var snapshot in result.Value.Queues)
                    {
                        OutboxProcessingMetrics.RecordSnapshot(snapshot);
                    }
                }
                else
                {
                    logger.LogWarning("Unable to collect background outbox queue metrics: {Error}", result.FirstError);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Background outbox metric collection failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
