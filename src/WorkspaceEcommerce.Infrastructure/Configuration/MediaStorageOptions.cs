namespace WorkspaceEcommerce.Infrastructure.Configuration;

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    public string Provider { get; init; } = "Local";
    public string PublicBaseUrl { get; init; } = "http://localhost:5080";
    public string? LocalRootPath { get; init; }
    public string? Bucket { get; init; }
    public string? ServiceUrl { get; init; }
    public string? Region { get; init; }
    public string? AccessKey { get; init; }
    public string? SecretKey { get; init; }
    public bool ForcePathStyle { get; init; } = true;
    public long MaxUploadBytes { get; init; } = 5 * 1024 * 1024;
    public int MaxWidth { get; init; } = 4096;
    public int MaxHeight { get; init; } = 4096;
    public long MaxPixels { get; init; } = 16_000_000;
    public int CleanupRetentionHours { get; init; } = 24;
}
