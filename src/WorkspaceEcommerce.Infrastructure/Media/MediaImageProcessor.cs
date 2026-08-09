using System.Security.Cryptography;
using ImageMagick;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Media;

internal sealed class MediaImageProcessor(MediaStorageOptions options)
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif"
        };

    public async Task<Result<ProcessedMediaImage>> ProcessAsync(
        Stream content,
        string originalFileName,
        string declaredContentType,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        if (declaredLength is <= 0 or > 25 * 1024 * 1024 || declaredLength > options.MaxUploadBytes)
        {
            return Result<ProcessedMediaImage>.Validation(["The image exceeds the configured upload limit."]);
        }

        await using var source = new MemoryStream();
        await content.CopyToAsync(source, cancellationToken);
        if (source.Length is 0 or > 25 * 1024 * 1024 || source.Length > options.MaxUploadBytes)
        {
            return Result<ProcessedMediaImage>.Validation(["The image exceeds the configured upload limit."]);
        }

        var sourceBytes = source.ToArray();
        var detectedContentType = DetectContentType(sourceBytes);
        if (detectedContentType is null)
        {
            return Result<ProcessedMediaImage>.Validation(["The uploaded file is not a supported image."]);
        }

        if (string.Equals(detectedContentType, "image/gif", StringComparison.Ordinal))
        {
            return Result<ProcessedMediaImage>.Validation(["GIF uploads are not supported. Upload a JPEG, PNG, or WebP image instead."]);
        }

        if (!string.Equals(NormalizeContentType(declaredContentType), detectedContentType, StringComparison.Ordinal))
        {
            return Result<ProcessedMediaImage>.Validation(["The declared image type does not match the file contents."]);
        }

        var extension = Path.GetExtension(originalFileName);
        if (!string.IsNullOrWhiteSpace(extension) &&
            (!ExtensionContentTypes.TryGetValue(extension, out var extensionContentType) ||
             !string.Equals(extensionContentType, detectedContentType, StringComparison.Ordinal)))
        {
            return Result<ProcessedMediaImage>.Validation(["The image filename extension does not match the file contents."]);
        }

        try
        {
            using var frames = new MagickImageCollection(sourceBytes);
            if (frames.Count != 1)
            {
                return Result<ProcessedMediaImage>.Validation(["Animated or multi-frame images are not supported."]);
            }

            using var image = frames[0];

            if (image.Width > options.MaxWidth || image.Height > options.MaxHeight ||
                (long)image.Width * image.Height > options.MaxPixels)
            {
                return Result<ProcessedMediaImage>.Validation(["The decoded image dimensions exceed the configured limit."]);
            }

            image.AutoOrient();
            image.Strip();

            var canonical = await EncodeWebpAsync(image, cancellationToken);
            var variants = new List<ProcessedMediaVariant>();
            foreach (var targetWidth in new[] { 320, 800, 1600 })
            {
                if (targetWidth >= image.Width)
                {
                    continue;
                }

                using var variant = image.Clone();
                variant.Resize(new MagickGeometry((uint)targetWidth, (uint)targetWidth));
                variants.Add(new ProcessedMediaVariant(
                    $"w{targetWidth}",
                    checked((int)variant.Width),
                    checked((int)variant.Height),
                    await EncodeWebpAsync(variant, cancellationToken)));
            }

            return Result<ProcessedMediaImage>.Success(new ProcessedMediaImage(
                checked((int)image.Width),
                checked((int)image.Height),
                canonical,
                Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant(),
                variants));
        }
        catch (MagickException)
        {
            return Result<ProcessedMediaImage>.Validation(["The uploaded image content is malformed."]);
        }
    }

    private static Task<byte[]> EncodeWebpAsync(IMagickImage image, CancellationToken cancellationToken)
    {
        image.Format = MagickFormat.WebP;
        image.Quality = 82;
        return Task.FromResult(image.ToByteArray(MagickFormat.WebP));
    }

    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return "image/png";
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
        {
            return "image/gif";
        }

        return null;
    }

    private static string NormalizeContentType(string value) => value.Split(';', 2)[0].Trim().ToLowerInvariant();
}

internal sealed record ProcessedMediaImage(
    int Width,
    int Height,
    byte[] CanonicalContent,
    string Checksum,
    IReadOnlyCollection<ProcessedMediaVariant> Variants);

internal sealed record ProcessedMediaVariant(string Name, int Width, int Height, byte[] Content);
