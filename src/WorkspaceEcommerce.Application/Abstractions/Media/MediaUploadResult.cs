namespace WorkspaceEcommerce.Application.Abstractions.Media;

public sealed record MediaUploadResult(
    string Url,
    string FileName,
    string ContentType,
    long Size,
    string ObjectKey,
    int Width,
    int Height,
    string Checksum,
    IReadOnlyCollection<MediaVariant> Variants);

public sealed record MediaVariant(
    string Name,
    string Url,
    string ObjectKey,
    int Width,
    int Height,
    long Size);

public sealed record MediaObjectMetadata(
    string Url,
    string ObjectKey,
    string ContentType,
    long Size,
    int Width,
    int Height,
    string Checksum,
    DateTimeOffset CreatedAt);
