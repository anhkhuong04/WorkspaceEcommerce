using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Shipping;

internal sealed class ShippingQuoteCleanupWorker(IServiceScopeFactory scopes, TimeProvider clock,
    IOptions<ShippingCheckoutOptions> options, ILogger<ShippingQuoteCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = clock.GetUtcNow().AddDays(-options.Value.UnusedQuoteRetentionDays);
                // Consumed quotes are historical order evidence and are never removed here.
                await db.Set<ShippingQuote>().Where(x => x.OrderId == null && x.ExpiresAtUtc < cutoff)
                    .OrderBy(x => x.ExpiresAtUtc).Take(1000).ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(exception, "Unused shipping quote retention failed."); }
        }
    }
}
