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
    public async Task<Result<PagedResult<StockAdjustmentDto>>> GetStockHistoryAsync(Guid id, int pageNumber, CancellationToken cancellationToken = default)
    {
        if (pageNumber is < 1 or > 100000) return Result<PagedResult<StockAdjustmentDto>>.Validation(["Invalid history page."]);
        if (!await catalogStore.ProductVariants.Where(v => v.Id == id).AnyAsyncSafe(cancellationToken)) return Result<PagedResult<StockAdjustmentDto>>.NotFound("SKU was not found.");
        var query = catalogStore.StockAdjustments.AsNoTrackingIfEf().Where(a => a.ProductVariantId == id);
        var count = await query.CountAsyncSafe(cancellationToken);
        var items = await query.OrderByDescending(a => a.OccurredAt).ThenBy(a => a.Id).Skip((pageNumber - 1) * 20).Take(20)
            .Select(a => new StockAdjustmentDto(a.Id, a.BeforeQuantity, a.AfterQuantity, a.Reason, a.Actor, a.OccurredAt)).ToArrayAsyncSafe(cancellationToken);
        return Result<PagedResult<StockAdjustmentDto>>.Success(new(items, pageNumber, 20, count));
    }
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

            variant.UpdateDimensions(request.WeightKg, request.LengthCm, request.WidthCm, request.HeightCm);
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
        CancellationToken cancellationToken = default, string actor = "Admin")
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductVariantDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        Result<AdminProductVariantDto>? result = null;
        await writeStore.ExecuteInTransactionAsync(async ct => { result = await UpdateLockedAsync(id, request, actor, ct); }, cancellationToken);
        return result!;
    }

    private async Task<Result<AdminProductVariantDto>> UpdateLockedAsync(Guid id, UpdateProductVariantRequest request, string actor, CancellationToken cancellationToken)
    {
        var variant = await catalogStore.FindVariantForUpdateAsync(id, cancellationToken);
        if (variant is null)
        {
            return Result<AdminProductVariantDto>.NotFound("Product variant was not found.");
        }

        if ((request.ExpectedStockQuantity.HasValue && request.ExpectedStockQuantity != variant.StockQuantity) ||
            (!request.ExpectedStockQuantity.HasValue && request.StockQuantity != variant.StockQuantity))
            return Result<AdminProductVariantDto>.Conflict("Stock changed or its expected quantity is missing. Reload the SKU before saving.");
        var beforeQuantity = variant.StockQuantity;
        if (request.StockQuantity != beforeQuantity && (string.IsNullOrWhiteSpace(request.StockChangeReason) || request.StockChangeReason.Trim().Length > 500 || string.IsNullOrWhiteSpace(actor) || actor.Length > 256))
            return Result<AdminProductVariantDto>.Validation(["A stock adjustment reason of up to 500 characters is required."]);

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
            if (request.UpdateDimensions) variant.UpdateDimensions(request.WeightKg, request.LengthCm, request.WidthCm, request.HeightCm);
            variant.UpdateStock(request.StockQuantity);
            if (beforeQuantity != request.StockQuantity) writeStore.Add(new StockAdjustment(Guid.NewGuid(), id, beforeQuantity, request.StockQuantity, request.StockChangeReason!.Trim(), actor, DateTimeOffset.UtcNow));
            SetActive(variant, request.IsActive);

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
            variant.IsActive) { WeightKg = variant.WeightKg, LengthCm = variant.LengthCm, WidthCm = variant.WidthCm, HeightCm = variant.HeightCm };
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
