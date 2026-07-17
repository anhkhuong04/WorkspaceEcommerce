namespace WorkspaceEcommerce.Application.Abstractions.Media;

public sealed record MediaUploadRequest(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length,
    string? Folder);
