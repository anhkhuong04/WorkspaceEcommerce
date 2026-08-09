namespace WorkspaceEcommerce.Infrastructure.Media;

internal interface IMediaObjectStore
{
    Task PutAsync(string objectKey, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);
}
