using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Shipping;

internal sealed class OrderExpiryWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<OrderExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IOrderExpiryService>().ExpireDueAsync(100, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(exception, "Payment deadline expiry failed; the next sweep will retry."); }
    }
}
