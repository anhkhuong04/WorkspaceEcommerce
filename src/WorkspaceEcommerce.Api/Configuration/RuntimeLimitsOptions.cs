using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace WorkspaceEcommerce.Api.Configuration;

/// <summary>
/// Bounded process-level limits for HTTP parsing and graceful termination.
/// Endpoint-specific limits remain stricter where appropriate (for example,
/// webhook and media upload endpoints).
/// </summary>
public sealed class RuntimeLimitsOptions
{
    public const string SectionName = "RuntimeLimits";

    public long MaxRequestBodyBytes { get; set; } = 6 * 1024 * 1024;

    public int MaxRequestLineBytes { get; set; } = 8 * 1024;

    public int MaxRequestHeadersBytes { get; set; } = 32 * 1024;

    public int MaxRequestHeaderCount { get; set; } = 100;

    public int KeepAliveTimeoutSeconds { get; set; } = 120;

    public int RequestHeadersTimeoutSeconds { get; set; } = 30;

    public int ShutdownTimeoutSeconds { get; set; } = 30;

    public void Validate()
    {
        EnsureRange(nameof(MaxRequestBodyBytes), MaxRequestBodyBytes, 1 * 1024 * 1024, 64 * 1024 * 1024);
        EnsureRange(nameof(MaxRequestLineBytes), MaxRequestLineBytes, 1024, 16 * 1024);
        EnsureRange(nameof(MaxRequestHeadersBytes), MaxRequestHeadersBytes, 8 * 1024, 64 * 1024);
        EnsureRange(nameof(MaxRequestHeaderCount), MaxRequestHeaderCount, 16, 200);
        EnsureRange(nameof(KeepAliveTimeoutSeconds), KeepAliveTimeoutSeconds, 5, 300);
        EnsureRange(nameof(RequestHeadersTimeoutSeconds), RequestHeadersTimeoutSeconds, 5, 120);
        EnsureRange(nameof(ShutdownTimeoutSeconds), ShutdownTimeoutSeconds, 5, 120);
    }

    public void ApplyTo(KestrelServerOptions kestrelOptions)
    {
        ArgumentNullException.ThrowIfNull(kestrelOptions);

        kestrelOptions.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
        kestrelOptions.Limits.MaxRequestLineSize = MaxRequestLineBytes;
        kestrelOptions.Limits.MaxRequestHeadersTotalSize = MaxRequestHeadersBytes;
        kestrelOptions.Limits.MaxRequestHeaderCount = MaxRequestHeaderCount;
        kestrelOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(KeepAliveTimeoutSeconds);
        kestrelOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(RequestHeadersTimeoutSeconds);
    }

    private static void EnsureRange(string name, long value, long minimum, long maximum)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:{name}' must be between {minimum} and {maximum}.");
        }
    }
}
