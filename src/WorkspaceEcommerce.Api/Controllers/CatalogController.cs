using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class CatalogController(IStorefrontCatalogService catalogService) : ControllerBase
{
    [HttpGet("api/categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<StorefrontCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var result = await catalogService.GetCategoriesAsync(cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/products")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StorefrontProductListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] ProductListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.GetProductsAsync(request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/products/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProductBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.GetProductBySlugAsync(slug, cancellationToken);

        return this.ToApiResponse(result);
    }
}
