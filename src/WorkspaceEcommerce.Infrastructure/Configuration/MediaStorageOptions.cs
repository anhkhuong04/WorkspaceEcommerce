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

    /// <summary>
    /// Scanner implementation selected for uploads. The current runtime only
    /// ships the explicit <c>NoOp</c> implementation; non-Development use is
    /// guarded by a time-bounded risk acceptance until a real scanner is
    /// integrated.
    /// </summary>
    public string MalwareScannerProvider { get; init; } = "NoOp";

    /// <summary>
    /// Named accountable owner for a non-Development exception that allows the
    /// no-op scanner. This is metadata, never a secret.
    /// </summary>
    public string? NoOpMalwareScannerRiskOwner { get; init; }

    /// <summary>
    /// UTC expiry for the no-op scanner risk acceptance. Uploads fail closed
    /// after this instant, including in a process that has not restarted.
    /// </summary>
    public DateTimeOffset? NoOpMalwareScannerRiskExpiresAtUtc { get; init; }

    /// <summary>
    /// Identifier of the externally recorded security risk acceptance (for
    /// example a risk-register or incident ticket reference). It must not
    /// contain credentials or upload content.
    /// </summary>
    public string? NoOpMalwareScannerRiskReference { get; init; }
}
