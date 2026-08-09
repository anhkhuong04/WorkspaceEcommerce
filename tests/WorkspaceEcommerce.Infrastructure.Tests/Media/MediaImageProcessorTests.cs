using ImageMagick;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Media;

namespace WorkspaceEcommerce.Infrastructure.Tests.Media;

public sealed class MediaImageProcessorTests
{
    private static readonly MediaStorageOptions Options = new()
    {
        PublicBaseUrl = "https://assets.example.test",
        MaxUploadBytes = 5 * 1024 * 1024,
        MaxWidth = 4096,
        MaxHeight = 4096,
        MaxPixels = 16_000_000
    };

    [Fact]
    public async Task ProcessAsync_ValidPng_NormalizesToWebpAndCreatesVariants()
    {
        using var image = new MagickImage(MagickColors.Red, 1000, 500);
        var source = image.ToByteArray(MagickFormat.Png);
        var processor = new MediaImageProcessor(Options);

        await using var stream = new MemoryStream(source);
        var result = await processor.ProcessAsync(stream, "banner.png", "image/png", source.Length, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000, result.Value!.Width);
        Assert.Equal(500, result.Value.Height);
        Assert.NotEmpty(result.Value.CanonicalContent);
        Assert.Contains(result.Value.Variants, variant => variant.Name == "w320");
        Assert.Contains(result.Value.Variants, variant => variant.Name == "w800");
    }

    [Fact]
    public async Task ProcessAsync_ValidJpegAndWebp_NormalizesBothToWebp()
    {
        var processor = new MediaImageProcessor(Options);
        foreach (var (format, fileName, contentType) in new[]
        {
            (MagickFormat.Jpeg, "photo.jpg", "image/jpeg"),
            (MagickFormat.WebP, "photo.webp", "image/webp")
        })
        {
            using var image = new MagickImage(MagickColors.Green, 40, 20);
            var source = image.ToByteArray(format);
            await using var stream = new MemoryStream(source);

            var result = await processor.ProcessAsync(stream, fileName, contentType, source.Length, CancellationToken.None);

            Assert.True(result.IsSuccess);
            using var canonical = new MagickImage(result.Value!.CanonicalContent);
            Assert.Equal(MagickFormat.WebP, canonical.Format);
        }
    }

    [Fact]
    public async Task ProcessAsync_SpoofedExtensionOrContentType_IsRejected()
    {
        using var image = new MagickImage(MagickColors.Blue, 10, 10);
        var source = image.ToByteArray(MagickFormat.Png);
        var processor = new MediaImageProcessor(Options);

        await using var stream = new MemoryStream(source);
        var result = await processor.ProcessAsync(stream, "payload.jpg", "image/jpeg", source.Length, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_Gif_IsRejectedByPolicy()
    {
        using var image = new MagickImage(MagickColors.Blue, 10, 10);
        var source = image.ToByteArray(MagickFormat.Gif);
        var processor = new MediaImageProcessor(Options);

        await using var stream = new MemoryStream(source);
        var result = await processor.ProcessAsync(stream, "animation.gif", "image/gif", source.Length, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ProcessAsync_MalformedOrOversizedDecodedImage_IsRejected()
    {
        var processor = new MediaImageProcessor(Options);
        await using var malformed = new MemoryStream("not an image"u8.ToArray());
        var malformedResult = await processor.ProcessAsync(malformed, "payload.png", "image/png", malformed.Length, CancellationToken.None);

        using var image = new MagickImage(MagickColors.Black, 4097, 1);
        var oversizedBytes = image.ToByteArray(MagickFormat.Png);
        await using var oversized = new MemoryStream(oversizedBytes);
        var oversizedResult = await processor.ProcessAsync(oversized, "wide.png", "image/png", oversizedBytes.Length, CancellationToken.None);

        Assert.False(malformedResult.IsSuccess);
        Assert.False(oversizedResult.IsSuccess);
    }
}
