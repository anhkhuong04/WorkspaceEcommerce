using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Authentication;

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
                await using var scope = scopeFactory.CreateAsyncScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<CustomerAccountCleanupService>();
                await cleanupService.CleanupAsync(stoppingToken);
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
}
