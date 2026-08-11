namespace WorkspaceEcommerce.Application.Abstractions.Warranties;

/// <summary>
/// Runtime controls for the additive warranty module. Production must keep
/// public lookup disabled until stock provisioning and abuse monitoring are live.
/// </summary>
public sealed class WarrantyOptions
{
    public const string SectionName = "Warranty";

    public bool Enabled { get; init; }

    public bool AdminEnabled { get; init; }

    public bool ActivationEnabled { get; init; }

    public bool PublicLookupEnabled { get; init; }

    public int IdentifierKeyVersion { get; init; } = 1;

    public string IdentifierHmacKey { get; init; } = string.Empty;

    /// <summary>
    /// Optional previous (or externally managed current) HMAC keys keyed by
    /// version. During a rotation retain the preceding key here so lookups can
    /// find records that have not yet been re-fingerprinted. New imports always
    /// use <see cref="IdentifierKeyVersion"/>.
    /// </summary>
    public Dictionary<int, string> IdentifierHmacKeys { get; init; } = [];

    public int MaxImportRows { get; init; } = 10_000;

    public IReadOnlyCollection<int> LookupKeyVersions => IdentifierHmacKeys.Keys
        .Append(IdentifierKeyVersion)
        .Where(version => version > 0)
        .Distinct()
        .OrderByDescending(version => version)
        .ToArray();
}
