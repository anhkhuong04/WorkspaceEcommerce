using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Catalog.Products;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize]
public sealed class ProductsController(IAdminProductService productService) : ControllerBase
{
    [HttpGet("api/admin/products")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AdminProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
    {
        var result = await productService.GetProductsAsync(cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/products")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.CreateProductAsync(request, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPut("api/admin/products/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.UpdateProductAsync(id, request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpDelete("api/admin/products/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        var result = await productService.DeleteProductAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/products/{id:guid}/variants")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductVariantDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateVariant(
        Guid id,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.CreateVariantAsync(id, request, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPut("api/admin/variants/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateVariant(
        Guid id,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.UpdateVariantAsync(id, request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/products/{id:guid}/images")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductImageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateImage(
        Guid id,
        [FromBody] CreateProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.CreateImageAsync(id, request, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPut("api/admin/product-images/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateImage(
        Guid id,
        [FromBody] UpdateProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.UpdateImageAsync(id, request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpDelete("api/admin/product-images/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteImage(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await productService.DeleteImageAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/products/{id:guid}/specifications")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductSpecificationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateSpecification(
        Guid id,
        [FromBody] CreateProductSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.CreateSpecificationAsync(id, request, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPut("api/admin/product-specifications/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductSpecificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSpecification(
        Guid id,
        [FromBody] UpdateProductSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.UpdateSpecificationAsync(id, request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpDelete("api/admin/product-specifications/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductSpecificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSpecification(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await productService.DeleteSpecificationAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }
}
