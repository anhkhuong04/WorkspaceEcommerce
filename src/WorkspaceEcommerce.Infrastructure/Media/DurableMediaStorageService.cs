using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Media;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Media;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Media;

internal class DurableMediaStorageService(
    AppDbContext dbContext,
    IMediaObjectStore objectStore,
    IMediaMalwareScanner malwareScanner,
    MediaImageProcessor imageProcessor,
    MediaStorageOptions options) : IMediaStorageService
{
    public async Task<Result<MediaUploadResult>> SaveAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var processed = await imageProcessor.ProcessAsync(
            request.Content,
            request.OriginalFileName,
            request.ContentType,
            request.Length,
            cancellationToken);
        if (!processed.IsSuccess)
        {
            return Result<MediaUploadResult>.Validation(processed.Errors);
        }

        var image = processed.Value!;
        if (await malwareScanner.ScanAsync(image.CanonicalContent, cancellationToken) is not MediaScanResult.Clean)
        {
            return Result<MediaUploadResult>.Failure(["The upload could not be cleared for publishing."]);
        }

        string folder;
        try
        {
            folder = NormalizeFolder(request.Folder);
        }
        catch (InvalidOperationException exception)
        {
            return Result<MediaUploadResult>.Validation([exception.Message]);
        }
        var id = Guid.NewGuid();
        var objectKey = $"{folder}/{id:N}/original.webp";
        var publicUrl = BuildPublicUrl(objectKey);
        var asset = new MediaAsset(
            id,
            folder,
            objectKey,
            publicUrl,
            "image/webp",
            image.Checksum,
            image.CanonicalContent.LongLength,
            image.Width,
            image.Height,
            1,
            request.OwnerIdentity);
        foreach (var variant in image.Variants)
        {
            var variantKey = $"{folder}/{id:N}/{variant.Name}.webp";
            asset.AddVariant(variant.Name, variantKey, BuildPublicUrl(variantKey), variant.Width, variant.Height, variant.Content.LongLength);
        }

        dbContext.MediaAssets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);

        var writtenKeys = new List<string>();
        try
        {
            await objectStore.PutAsync(objectKey, image.CanonicalContent, "image/webp", cancellationToken);
            writtenKeys.Add(objectKey);
            foreach (var variant in asset.Variants)
            {
                var bytes = image.Variants.Single(value => value.Name == variant.Name).Content;
                await objectStore.PutAsync(variant.ObjectKey, bytes, "image/webp", cancellationToken);
                writtenKeys.Add(variant.ObjectKey);
            }
        }
        catch (Exception exception)
        {
            foreach (var key in writtenKeys)
            {
                try { await objectStore.DeleteAsync(key, cancellationToken); } catch { /* cleanup is retried by the worker */ }
            }

            asset.MarkFailed("Object storage write failed: " + exception.GetType().Name);
            await TryPersistStateAsync(cancellationToken);
            return Result<MediaUploadResult>.Failure(["The image could not be stored. Please retry."]);
        }

        asset.MarkAvailable();
        try
        {
            await PersistAvailabilityAsync(cancellationToken);
        }
        catch
        {
            // Storage is intentionally retained as a pending, observable cleanup candidate.
            return Result<MediaUploadResult>.Failure(["The image was stored but is not yet available. Please retry later."]);
        }

        return Result<MediaUploadResult>.Success(new MediaUploadResult(
            asset.PublicUrl,
            Path.GetFileName(request.OriginalFileName),
            asset.ContentType,
            asset.Size,
            asset.ObjectKey,
            asset.Width,
            asset.Height,
            asset.Checksum,
            asset.Variants.Select(variant => new MediaVariant(
                variant.Name,
                variant.PublicUrl,
                variant.ObjectKey,
                variant.Width,
                variant.Height,
                variant.Size)).ToArray()));
    }

    public async Task<Result> DeleteIfUnreferencedAsync(string publicUrl, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.Include(value => value.Variants)
            .FirstOrDefaultAsync(value => value.PublicUrl == publicUrl && value.State != MediaAssetState.Deleted, cancellationToken);
        if (asset is null)
        {
            return Result.Success();
        }

        if (await IsReferencedAsync(publicUrl, cancellationToken))
        {
            return Result.Success();
        }

        await DeleteAssetObjectsAsync(asset, cancellationToken);
        asset.MarkDeleted();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<MediaObjectMetadata>> ReadMetadataAsync(string publicUrl, CancellationToken cancellationToken = default)
    {
        var asset = await dbContext.MediaAssets.AsNoTracking()
            .FirstOrDefaultAsync(value => value.PublicUrl == publicUrl && value.State == MediaAssetState.Available, cancellationToken);
        if (asset is null)
        {
            return Result<MediaObjectMetadata>.NotFound("Media asset was not found.");
        }

        return Result<MediaObjectMetadata>.Success(new MediaObjectMetadata(
            asset.PublicUrl, asset.ObjectKey, asset.ContentType, asset.Size, asset.Width, asset.Height, asset.Checksum, asset.CreatedAt));
    }

    internal async Task<bool> IsReferencedAsync(string publicUrl, CancellationToken cancellationToken)
    {
        return await dbContext.ProductImages.AnyAsync(image => image.ImageUrl == publicUrl, cancellationToken) ||
               await dbContext.Banners.AnyAsync(banner => banner.ImageUrl == publicUrl, cancellationToken) ||
               await dbContext.BlogPosts.AnyAsync(post => post.ImageUrl == publicUrl, cancellationToken);
    }

    internal async Task DeleteAssetObjectsAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        await objectStore.DeleteAsync(asset.ObjectKey, cancellationToken);
        foreach (var variant in asset.Variants)
        {
            await objectStore.DeleteAsync(variant.ObjectKey, cancellationToken);
        }
    }

    private async Task TryPersistStateAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); } catch { /* worker can still identify the original pending record */ }
    }

    protected virtual Task PersistAvailabilityAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private string BuildPublicUrl(string objectKey) =>
        options.PublicBaseUrl.TrimEnd('/') + "/media/" + objectKey;

    private static string NormalizeFolder(string? folder)
    {
        var normalized = string.IsNullOrWhiteSpace(folder) ? "general" : folder.Trim().ToLowerInvariant();
        if (normalized.Length > 64 || normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new InvalidOperationException("The media folder contains unsupported characters.");
        }

        return normalized;
    }
}
