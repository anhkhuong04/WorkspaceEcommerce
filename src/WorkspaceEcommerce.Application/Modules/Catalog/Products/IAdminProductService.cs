using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public interface IAdminProductService
{
    Task<Result<IReadOnlyCollection<AdminProductDto>>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<Result<AdminProductDto>> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductDto>> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductVariantDto>> CreateVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductVariantDto>> UpdateVariantAsync(
        Guid id,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default);

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
