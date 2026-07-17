namespace WorkspaceEcommerce.Application.Abstractions.Media;

public sealed record MediaUploadResult(
    string Url,
    string FileName,
    string ContentType,
    long Size);
