using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

internal sealed class AdminProductService(
    IAppDbContext dbContext,
    IValidator<CreateProductRequest> createProductValidator,
    IValidator<UpdateProductRequest> updateProductValidator,
    IValidator<CreateProductVariantRequest> createVariantValidator,
    IValidator<UpdateProductVariantRequest> updateVariantValidator) : IAdminProductService
{
    public Task<Result<IReadOnlyCollection<AdminProductDto>>> GetProductsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var categoriesById = dbContext.Categories.ToDictionary(category => category.Id);
        var variantsByProductId = dbContext.ProductVariants
            .OrderBy(variant => variant.Sku)
            .ToLookup(variant => variant.ProductId);

        var products = dbContext.Products
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Slug)
            .Select(product => ToDto(product, categoriesById, variantsByProductId[product.Id]))
            .ToArray();

        return Task.FromResult(Result<IReadOnlyCollection<AdminProductDto>>.Success(products));
    }

    public async Task<Result<AdminProductDto>> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createProductValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        if (!CategoryExists(request.CategoryId))
        {
            return Result<AdminProductDto>.Validation(["Product category does not exist."]);
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        if (ProductSlugExists(normalizedSlug))
        {
            return Result<AdminProductDto>.Conflict("Product slug already exists.");
        }

        var product = new Product(
            Guid.NewGuid(),
            request.CategoryId,
            request.Name,
            normalizedSlug,
            request.Description,
            request.IsFeatured,
            request.IsActive);

        dbContext.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminProductDto>.Success(ToDto(product, GetCategoriesById(), []));
    }

    public async Task<Result<AdminProductDto>> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateProductValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var product = dbContext.Products.FirstOrDefault(existing => existing.Id == id);
        if (product is null)
        {
            return Result<AdminProductDto>.NotFound("Product was not found.");
        }

        if (!CategoryExists(request.CategoryId))
        {
            return Result<AdminProductDto>.Validation(["Product category does not exist."]);
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        if (ProductSlugExists(normalizedSlug, id))
        {
            return Result<AdminProductDto>.Conflict("Product slug already exists.");
        }

        product.UpdateDetails(request.CategoryId, request.Name, normalizedSlug, request.Description);
        SetFeatured(product, request.IsFeatured);
        SetActive(product, request.IsActive);

        dbContext.Update(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        var variants = dbContext.ProductVariants.Where(variant => variant.ProductId == product.Id).ToArray();

        return Result<AdminProductDto>.Success(ToDto(product, GetCategoriesById(), variants));
    }

    public async Task<Result<AdminProductVariantDto>> CreateVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createVariantValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductVariantDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var product = dbContext.Products.FirstOrDefault(existing => existing.Id == productId);
        if (product is null)
        {
            return Result<AdminProductVariantDto>.NotFound("Product was not found.");
        }

        var normalizedSku = NormalizeSku(request.Sku);
        if (SkuExists(normalizedSku))
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

            dbContext.Update(product);
            dbContext.Add(variant);
            await dbContext.SaveChangesAsync(cancellationToken);

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
        var validationResult = await updateVariantValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductVariantDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var variant = dbContext.ProductVariants.FirstOrDefault(existing => existing.Id == id);
        if (variant is null)
        {
            return Result<AdminProductVariantDto>.NotFound("Product variant was not found.");
        }

        var normalizedSku = NormalizeSku(request.Sku);
        if (SkuExists(normalizedSku, id))
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

            dbContext.Update(variant);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminProductVariantDto>.Success(ToDto(variant));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductVariantDto>.Validation([exception.Message]);
        }
    }

    private Dictionary<Guid, Category> GetCategoriesById()
    {
        return dbContext.Categories.ToDictionary(category => category.Id);
    }

    private bool CategoryExists(Guid id)
    {
        return dbContext.Categories.Any(category => category.Id == id);
    }

    private bool ProductSlugExists(string slug, Guid? excludedProductId = null)
    {
        return dbContext.Products.Any(product =>
            product.Slug == slug &&
            (excludedProductId == null || product.Id != excludedProductId.Value));
    }

    private bool SkuExists(string sku, Guid? excludedVariantId = null)
    {
        return dbContext.ProductVariants.Any(variant =>
            string.Equals(variant.Sku, sku, StringComparison.OrdinalIgnoreCase) &&
            (excludedVariantId == null || variant.Id != excludedVariantId.Value));
    }

    private static AdminProductDto ToDto(
        Product product,
        IReadOnlyDictionary<Guid, Category> categoriesById,
        IEnumerable<ProductVariant> variants)
    {
        categoriesById.TryGetValue(product.CategoryId, out var category);

        return new AdminProductDto(
            product.Id,
            product.CategoryId,
            category?.Name,
            product.Name,
            product.Slug,
            product.Description,
            product.IsFeatured,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt,
            variants.Select(ToDto).ToArray());
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

    private static void SetFeatured(Product product, bool isFeatured)
    {
        if (isFeatured)
        {
            product.MarkAsFeatured();
        }
        else
        {
            product.UnmarkAsFeatured();
        }
    }

    private static void SetActive(Product product, bool isActive)
    {
        if (isActive)
        {
            product.Activate();
        }
        else
        {
            product.Deactivate();
        }
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

    private static string NormalizeSlug(string slug)
    {
        return slug.Trim().ToLowerInvariant();
    }

    private static string NormalizeSku(string sku)
    {
        return sku.Trim().ToUpperInvariant();
    }
}
