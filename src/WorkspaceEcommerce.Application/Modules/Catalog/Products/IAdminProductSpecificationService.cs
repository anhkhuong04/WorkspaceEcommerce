using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public interface IAdminProductSpecificationService
{
    Task<Result<AdminProductSpecificationDto>> CreateSpecificationAsync(
        Guid productId,
        CreateProductSpecificationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductSpecificationDto>> UpdateSpecificationAsync(
        Guid id,
        UpdateProductSpecificationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductSpecificationDto>> DeleteSpecificationAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
