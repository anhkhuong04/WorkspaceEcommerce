using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public interface IAdminProductImageService
{
    Task<Result<AdminProductImageDto>> CreateImageAsync(
        Guid productId,
        CreateProductImageRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductImageDto>> UpdateImageAsync(
        Guid id,
        UpdateProductImageRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductImageDto>> DeleteImageAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
