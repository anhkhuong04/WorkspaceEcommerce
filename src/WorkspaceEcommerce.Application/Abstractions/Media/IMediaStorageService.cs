using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Abstractions.Media;

public interface IMediaStorageService
{
    Task<Result<MediaUploadResult>> SaveAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken = default);
}
