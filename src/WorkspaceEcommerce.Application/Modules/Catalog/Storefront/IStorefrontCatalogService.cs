using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

public interface IStorefrontCatalogService
{
    Task<Result<IReadOnlyCollection<StorefrontCategoryDto>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<StorefrontProductListItemDto>>> GetProductsAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StorefrontProductDetailDto>> GetProductBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
