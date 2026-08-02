using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Shipment;

internal sealed class ShipmentCommandWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MiniLogisticsOptions> options,
    ILogger<ShipmentCommandWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.CommandWorkerIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IOrderShipmentService>();
                var processed = await service.ProcessDueCommandsAsync(batchSize: 20, stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Processed {CommandCount} shipment outbox commands", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Shipment command worker iteration failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
