using Microsoft.Extensions.Hosting;
using WorkspaceEcommerce.Application.Abstractions.Documents;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Documents;

/// <summary>
/// Retrieves only media served by this deployment for receipt rendering. Product
/// image URLs can be edited by administrators, so arbitrary remote URLs must not
/// be dereferenced by the backend.
/// </summary>
internal sealed class OrderReceiptImageResolver(
    HttpClient httpClient,
    MediaStorageOptions mediaStorageOptions,
    IHostEnvironment environment) : IOrderReceiptImageResolver
{
    private const int MaxImagesPerReceipt = 20;
    private const int MaxImageBytes = 2 * 1024 * 1024;

    public async Task<IReadOnlyDictionary<string, byte[]>> ResolveAsync(
        IReadOnlyCollection<string> imageUrls,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var mediaBaseUri = new Uri(mediaStorageOptions.PublicBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);

        foreach (var imageUrl in imageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxImagesPerReceipt))
        {
            var demoImage = await TryReadDemoImageAsync(imageUrl, environment.ContentRootPath, cancellationToken);
            if (demoImage is not null)
            {
                results[imageUrl] = demoImage;
                continue;
            }

            if (!TryGetApprovedMediaUri(imageUrl, mediaBaseUri, out var imageUri))
            {
                continue;
            }

            try
            {
                using var response = await httpClient.GetAsync(
                    imageUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (!response.IsSuccessStatusCode ||
                    response.Content.Headers.ContentType?.MediaType is not { } contentType ||
                    !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                    response.Content.Headers.ContentLength is > MaxImageBytes)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var content = await ReadBoundedAsync(stream, cancellationToken);
                if (content is not null)
                {
                    results[imageUrl] = content;
                }
            }
            catch (HttpRequestException)
            {
                // A missing image should not prevent the customer from receiving a receipt.
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The short image-download timeout must not fail receipt generation.
            }
        }

        return results;
    }

    private static bool TryGetApprovedMediaUri(string imageUrl, Uri mediaBaseUri, out Uri imageUri)
    {
        imageUri = null!;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var parsedUri) ||
            !string.Equals(parsedUri.Scheme, mediaBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parsedUri.Host, mediaBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            parsedUri.Port != mediaBaseUri.Port)
        {
            return false;
        }

        var mediaPath = mediaBaseUri.AbsolutePath.TrimEnd('/') + "/media/";
        if (!parsedUri.AbsolutePath.StartsWith(mediaPath, StringComparison.Ordinal))
        {
            return false;
        }

        imageUri = parsedUri;
        return true;
    }

    private static async Task<byte[]?> TryReadDemoImageAsync(
        string imageUrl,
        string contentRootPath,
        CancellationToken cancellationToken)
    {
        const string demoPrefix = "/demo/";
        if (!imageUrl.StartsWith(demoPrefix, StringComparison.Ordinal) ||
            imageUrl.Contains('?', StringComparison.Ordinal) ||
            imageUrl.Contains('#', StringComparison.Ordinal))
        {
            return null;
        }

        var roots = new[]
        {
            Path.Combine(contentRootPath, "wwwroot", "demo"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "demo")
        };
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var demoRoot = Path.GetFullPath(root);
            var candidatePath = Path.GetFullPath(Path.Combine(demoRoot, imageUrl[demoPrefix.Length..]));
            if (!candidatePath.StartsWith(demoRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !File.Exists(candidatePath) ||
                new FileInfo(candidatePath).Length > MaxImageBytes)
            {
                continue;
            }

            return await File.ReadAllBytesAsync(candidatePath, cancellationToken);
        }

        return null;
    }

    private static async Task<byte[]?> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaxImageBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }
}
