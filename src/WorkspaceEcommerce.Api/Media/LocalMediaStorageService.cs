using WorkspaceEcommerce.Application.Abstractions.Media;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Api.Media;

internal sealed class LocalMediaStorageService(IWebHostEnvironment environment) : IMediaStorageService
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "products",
        "banners",
        "blogs",
        "general"
    };

    public async Task<Result<MediaUploadResult>> SaveAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Length == 0)
        {
            return Result<MediaUploadResult>.Validation(["Image file is required."]);
        }

        if (request.Length > MaxFileSize)
        {
            return Result<MediaUploadResult>.Validation(["Image file must be 5 MB or smaller."]);
        }

        if (!AllowedContentTypes.TryGetValue(request.ContentType, out var canonicalExtension))
        {
            return Result<MediaUploadResult>.Validation(["Only JPG, PNG, WEBP, and GIF images are supported."]);
        }

        var originalExtension = Path.GetExtension(request.OriginalFileName);
        if (!AllowedContentTypes.Values.Any(extension => string.Equals(extension, originalExtension, StringComparison.OrdinalIgnoreCase)) &&
            !string.Equals(originalExtension, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return Result<MediaUploadResult>.Validation(["Image file extension is not supported."]);
        }

        var folder = NormalizeFolder(request.Folder);
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        var uploadDirectory = Path.Combine(webRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{Guid.NewGuid():N}{canonicalExtension}";
        var targetPath = Path.Combine(uploadDirectory, storedFileName);

        await using (var target = File.Create(targetPath))
        {
            await request.Content.CopyToAsync(target, cancellationToken);
        }

        var url = $"/uploads/{folder}/{storedFileName}";
        return Result<MediaUploadResult>.Success(new MediaUploadResult(
            url,
            storedFileName,
            request.ContentType,
            request.Length));
    }

    private static string NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return "general";
        }

        var normalized = folder.Trim().ToLowerInvariant();
        return AllowedFolders.Contains(normalized) ? normalized : "general";
    }
}
