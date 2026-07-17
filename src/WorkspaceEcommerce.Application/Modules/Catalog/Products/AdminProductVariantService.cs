using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

internal sealed class AdminProductVariantService(
    ICatalogReadStore catalogStore,
    IAppWriteStore writeStore,
    IValidator<CreateProductVariantRequest> createValidator,
    IValidator<UpdateProductVariantRequest> updateValidator) : IAdminProductVariantService
{
    public async Task<Result<AdminProductVariantDto>> CreateVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductVariantDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var product = await catalogStore.Products
            .Where(existing => existing.Id == productId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (product is null)
        {
            return Result<AdminProductVariantDto>.NotFound("Product was not found.");
        }

        var normalizedSku = NormalizeSku(request.Sku);
        if (await SkuExistsAsync(normalizedSku, cancellationToken: cancellationToken))
        {
            return Result<AdminProductVariantDto>.Conflict("Product variant SKU already exists.");
        }

        try
        {
            var variant = product.AddVariant(
                Guid.NewGuid(),
                normalizedSku,
                request.Name,
                request.Color,
                request.Size,
                request.Price,
                request.CompareAtPrice,
                request.StockQuantity,
                request.RequiresInstallation,
                request.IsActive);

            writeStore.Update(product);
            writeStore.Add(variant);
            await writeStore.SaveChangesAsync(cancellationToken);

            return Result<AdminProductVariantDto>.Success(ToDto(variant));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductVariantDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductVariantDto>> UpdateVariantAsync(
        Guid id,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductVariantDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var variant = await catalogStore.ProductVariants
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (variant is null)
        {
            return Result<AdminProductVariantDto>.NotFound("Product variant was not found.");
        }

        var normalizedSku = NormalizeSku(request.Sku);
        if (await SkuExistsAsync(normalizedSku, id, cancellationToken))
        {
            return Result<AdminProductVariantDto>.Conflict("Product variant SKU already exists.");
        }

        try
        {
            variant.UpdateDetails(
                normalizedSku,
                request.Name,
                request.Color,
                request.Size,
                request.RequiresInstallation);
            variant.UpdatePricing(request.Price, request.CompareAtPrice);
            variant.UpdateStock(request.StockQuantity);
            SetActive(variant, request.IsActive);

            writeStore.Update(variant);
            await writeStore.SaveChangesAsync(cancellationToken);

            return Result<AdminProductVariantDto>.Success(ToDto(variant));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductVariantDto>.Validation([exception.Message]);
        }
    }

    private Task<bool> SkuExistsAsync(
        string sku,
        Guid? excludedVariantId = null,
        CancellationToken cancellationToken = default)
    {
        return catalogStore.ProductVariants
            .Where(variant =>
                variant.Sku == sku &&
                (excludedVariantId == null || variant.Id != excludedVariantId.Value))
            .AnyAsyncSafe(cancellationToken);
    }

    private static AdminProductVariantDto ToDto(ProductVariant variant)
    {
        return new AdminProductVariantDto(
            variant.Id,
            variant.ProductId,
            variant.Sku,
            variant.Name,
            variant.Color,
            variant.Size,
            variant.Price,
            variant.CompareAtPrice,
            variant.StockQuantity,
            variant.RequiresInstallation,
            variant.IsActive);
    }

    private static void SetActive(ProductVariant variant, bool isActive)
    {
        if (isActive)
        {
            variant.Activate();
        }
        else
        {
            variant.Deactivate();
        }
    }

    private static string NormalizeSku(string sku)
    {
        return sku.Trim().ToUpperInvariant();
    }
}
