using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Domain.Modules.Media;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Media;

internal sealed class MediaAssetCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IMediaObjectStore objectStore,
    MediaStorageOptions options,
    ILogger<MediaAssetCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                logger.LogError(exception, "Media cleanup worker iteration failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-options.CleanupRetentionHours);
        var candidates = await dbContext.MediaAssets
            .Include(asset => asset.Variants)
            .Where(asset => asset.State != MediaAssetState.Deleted && asset.CreatedAt < cutoff)
            .OrderBy(asset => asset.CreatedAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        foreach (var asset in candidates)
        {
            var referenced = await dbContext.ProductImages.AnyAsync(image => image.ImageUrl == asset.PublicUrl, cancellationToken) ||
                await dbContext.Banners.AnyAsync(banner => banner.ImageUrl == asset.PublicUrl, cancellationToken) ||
                await dbContext.BlogPosts.AnyAsync(post => post.ImageUrl == asset.PublicUrl, cancellationToken);
            if (referenced)
            {
                continue;
            }

            try
            {
                await objectStore.DeleteAsync(asset.ObjectKey, cancellationToken);
                foreach (var variant in asset.Variants)
                {
                    await objectStore.DeleteAsync(variant.ObjectKey, cancellationToken);
                }
                asset.MarkDeleted();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not delete unreferenced media asset {MediaAssetId}", asset.Id);
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
