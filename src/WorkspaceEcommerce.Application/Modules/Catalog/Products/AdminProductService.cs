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
    IValidator<UpdateProductVariantRequest> updateVariantValidator,
    IValidator<CreateProductImageRequest> createImageValidator,
    IValidator<UpdateProductImageRequest> updateImageValidator,
    IValidator<CreateProductSpecificationRequest> createSpecificationValidator,
    IValidator<UpdateProductSpecificationRequest> updateSpecificationValidator) : IAdminProductService
{
    public Task<Result<IReadOnlyCollection<AdminProductDto>>> GetProductsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var categoriesById = dbContext.Categories.ToDictionary(category => category.Id);
        var variantsByProductId = dbContext.ProductVariants
            .OrderBy(variant => variant.Sku)
            .ToLookup(variant => variant.ProductId);
        var imagesByProductId = dbContext.ProductImages
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.ImageUrl)
            .ToLookup(image => image.ProductId);
        var specificationsByProductId = dbContext.ProductSpecifications
            .OrderBy(specification => specification.SortOrder)
            .ThenBy(specification => specification.Name)
            .ToLookup(specification => specification.ProductId);

        var products = dbContext.Products
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Slug)
            .Select(product => ToDto(
                product,
                categoriesById,
                variantsByProductId[product.Id],
                imagesByProductId[product.Id],
                specificationsByProductId[product.Id]))
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

        return Result<AdminProductDto>.Success(ToDto(product, GetCategoriesById(), [], [], []));
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
        var images = dbContext.ProductImages.Where(image => image.ProductId == product.Id).ToArray();
        var specifications = dbContext.ProductSpecifications
            .Where(specification => specification.ProductId == product.Id)
            .ToArray();

        return Result<AdminProductDto>.Success(ToDto(product, GetCategoriesById(), variants, images, specifications));
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

    public async Task<Result<AdminProductImageDto>> CreateImageAsync(
        Guid productId,
        CreateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createImageValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductImageDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var product = dbContext.Products.FirstOrDefault(existing => existing.Id == productId);
        if (product is null)
        {
            return Result<AdminProductImageDto>.NotFound("Product was not found.");
        }

        try
        {
            var image = product.AddImage(
                Guid.NewGuid(),
                request.ImageUrl,
                request.AltText,
                request.SortOrder);

            dbContext.Update(product);
            dbContext.Add(image);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminProductImageDto>.Success(ToDto(image));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductImageDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductImageDto>> UpdateImageAsync(
        Guid id,
        UpdateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateImageValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductImageDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var image = dbContext.ProductImages.FirstOrDefault(existing => existing.Id == id);
        if (image is null)
        {
            return Result<AdminProductImageDto>.NotFound("Product image was not found.");
        }

        try
        {
            image.Update(request.ImageUrl, request.AltText, request.SortOrder);

            dbContext.Update(image);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminProductImageDto>.Success(ToDto(image));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductImageDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductImageDto>> DeleteImageAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var image = dbContext.ProductImages.FirstOrDefault(existing => existing.Id == id);
        if (image is null)
        {
            return Result<AdminProductImageDto>.NotFound("Product image was not found.");
        }

        var dto = ToDto(image);
        dbContext.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminProductImageDto>.Success(dto);
    }

    public async Task<Result<AdminProductSpecificationDto>> CreateSpecificationAsync(
        Guid productId,
        CreateProductSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createSpecificationValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductSpecificationDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var product = dbContext.Products.FirstOrDefault(existing => existing.Id == productId);
        if (product is null)
        {
            return Result<AdminProductSpecificationDto>.NotFound("Product was not found.");
        }

        try
        {
            var specification = product.AddSpecification(
                Guid.NewGuid(),
                request.Name,
                request.Value,
                request.SortOrder);

            dbContext.Update(product);
            dbContext.Add(specification);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminProductSpecificationDto>.Success(ToDto(specification));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductSpecificationDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductSpecificationDto>> UpdateSpecificationAsync(
        Guid id,
        UpdateProductSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateSpecificationValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductSpecificationDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var specification = dbContext.ProductSpecifications.FirstOrDefault(existing => existing.Id == id);
        if (specification is null)
        {
            return Result<AdminProductSpecificationDto>.NotFound("Product specification was not found.");
        }

        try
        {
            specification.Update(request.Name, request.Value, request.SortOrder);

            dbContext.Update(specification);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminProductSpecificationDto>.Success(ToDto(specification));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductSpecificationDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductSpecificationDto>> DeleteSpecificationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var specification = dbContext.ProductSpecifications.FirstOrDefault(existing => existing.Id == id);
        if (specification is null)
        {
            return Result<AdminProductSpecificationDto>.NotFound("Product specification was not found.");
        }

        var dto = ToDto(specification);
        dbContext.Remove(specification);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminProductSpecificationDto>.Success(dto);
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
        IEnumerable<ProductVariant> variants,
        IEnumerable<ProductImage> images,
        IEnumerable<ProductSpecification> specifications)
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
            variants.Select(ToDto).ToArray(),
            images.Select(ToDto).ToArray(),
            specifications.Select(ToDto).ToArray());
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

    private static AdminProductImageDto ToDto(ProductImage image)
    {
        return new AdminProductImageDto(
            image.Id,
            image.ProductId,
            image.ImageUrl,
            image.AltText,
            image.SortOrder);
    }

    private static AdminProductSpecificationDto ToDto(ProductSpecification specification)
    {
        return new AdminProductSpecificationDto(
            specification.Id,
            specification.ProductId,
            specification.Name,
            specification.Value,
            specification.SortOrder);
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
