using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public interface IAdminProductVariantService
{
    Task<Result<AdminProductVariantDto>> CreateVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductVariantDto>> UpdateVariantAsync(
        Guid id,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default);
}
