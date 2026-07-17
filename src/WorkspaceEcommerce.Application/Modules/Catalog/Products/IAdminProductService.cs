using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public interface IAdminProductService
{
    Task<Result<PagedResult<AdminProductDto>>> GetProductsAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductDto>> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductDto>> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductDto>> DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
