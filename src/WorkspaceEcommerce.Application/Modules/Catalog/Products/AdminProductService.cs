using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

internal sealed class AdminProductService(
    ICatalogReadStore catalogStore,
    IOrderReadStore orderStore,
    IAppWriteStore writeStore,
    IValidator<CreateProductRequest> createProductValidator,
    IValidator<UpdateProductRequest> updateProductValidator) : IAdminProductService
{
    public async Task<Result<PagedResult<AdminProductDto>>> GetProductsAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var productsQuery = catalogStore.Products
            .OrderBy(product => product.Name["en"])
            .ThenBy(product => product.Slug);

        var totalCount = await productsQuery.CountAsyncSafe(cancellationToken);
        var products = await productsQuery
            .Skip(request.Skip)
            .Take(pageSize)
            .ToArrayAsyncSafe(cancellationToken);

        var productIds = products.Select(product => product.Id).ToArray();
        var categoryIds = products.Select(product => product.CategoryId).Distinct().ToArray();
        var categoriesById = await catalogStore.Categories
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsyncSafe(category => category.Id, cancellationToken);
        var variantsByProductId = (await catalogStore.ProductVariants
            .Where(variant => productIds.Contains(variant.ProductId))
            .OrderBy(variant => variant.Sku)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(variant => variant.ProductId);
        var imagesByProductId = (await catalogStore.ProductImages
            .Where(image => productIds.Contains(image.ProductId))
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.ImageUrl)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(image => image.ProductId);
        var specificationsByProductId = (await catalogStore.ProductSpecifications
            .Where(specification => productIds.Contains(specification.ProductId))
            .OrderBy(specification => specification.SortOrder)
            .ThenBy(specification => specification.Name)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(specification => specification.ProductId);
        var items = products
            .Select(product => ToDto(
                product,
                categoriesById,
                variantsByProductId[product.Id],
                imagesByProductId[product.Id],
                specificationsByProductId[product.Id]))
            .ToArray();

        return Result<PagedResult<AdminProductDto>>.Success(
            new PagedResult<AdminProductDto>(items, pageNumber, pageSize, totalCount));
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

        if (!await CategoryExistsAsync(request.CategoryId, cancellationToken))
        {
            return Result<AdminProductDto>.Validation(["Product category does not exist."]);
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        if (await ProductSlugExistsAsync(normalizedSlug, cancellationToken: cancellationToken))
        {
            return Result<AdminProductDto>.Conflict("Product slug already exists.");
        }

        var product = new Product(
            Guid.NewGuid(),
            request.CategoryId,
            new LocalizedText(request.Name),
            normalizedSlug,
            request.Description != null ? new LocalizedText(request.Description) : null,
            request.IsFeatured,
            request.IsActive);

        writeStore.Add(product);
        await writeStore.SaveChangesAsync(cancellationToken);

        return Result<AdminProductDto>.Success(ToDto(product, await GetCategoriesByIdAsync(cancellationToken), [], [], []));
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

        var product = await catalogStore.Products
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (product is null)
        {
            return Result<AdminProductDto>.NotFound("Product was not found.");
        }

        if (!await CategoryExistsAsync(request.CategoryId, cancellationToken))
        {
            return Result<AdminProductDto>.Validation(["Product category does not exist."]);
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        if (await ProductSlugExistsAsync(normalizedSlug, id, cancellationToken))
        {
            return Result<AdminProductDto>.Conflict("Product slug already exists.");
        }

        product.UpdateDetails(request.CategoryId, new LocalizedText(request.Name), normalizedSlug, request.Description != null ? new LocalizedText(request.Description) : null);
        SetFeatured(product, request.IsFeatured);
        SetActive(product, request.IsActive);

        writeStore.Update(product);
        await writeStore.SaveChangesAsync(cancellationToken);

        var variants = await catalogStore.ProductVariants
            .Where(variant => variant.ProductId == product.Id)
            .OrderBy(variant => variant.Sku)
            .ToArrayAsyncSafe(cancellationToken);
        var images = await catalogStore.ProductImages
            .Where(image => image.ProductId == product.Id)
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.ImageUrl)
            .ToArrayAsyncSafe(cancellationToken);
        var specifications = await catalogStore.ProductSpecifications
            .Where(specification => specification.ProductId == product.Id)
            .OrderBy(specification => specification.SortOrder)
            .ThenBy(specification => specification.Name)
            .ToArrayAsyncSafe(cancellationToken);

        return Result<AdminProductDto>.Success(ToDto(product, await GetCategoriesByIdAsync(cancellationToken), variants, images, specifications));
    }

    public async Task<Result<AdminProductDto>> DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await catalogStore.Products
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (product is null)
        {
            return Result<AdminProductDto>.NotFound("Product was not found.");
        }

        var variants = await catalogStore.ProductVariants
            .Where(variant => variant.ProductId == id)
            .ToArrayAsyncSafe(cancellationToken);
        var variantIds = variants.Select(variant => variant.Id).ToArray();
        if (await orderStore.OrderItems
            .Where(item => variantIds.Contains(item.ProductVariantId))
            .AnyAsyncSafe(cancellationToken))
        {
            return Result<AdminProductDto>.Conflict("Product has order history and cannot be deleted. Deactivate it instead.");
        }

        var images = await catalogStore.ProductImages
            .Where(image => image.ProductId == id)
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.ImageUrl)
            .ToArrayAsyncSafe(cancellationToken);
        var specifications = await catalogStore.ProductSpecifications
            .Where(specification => specification.ProductId == id)
            .OrderBy(specification => specification.SortOrder)
            .ThenBy(specification => specification.Name)
            .ToArrayAsyncSafe(cancellationToken);
        var dto = ToDto(product, await GetCategoriesByIdAsync(cancellationToken), variants, images, specifications);

        foreach (var image in images)
        {
            writeStore.Remove(image);
        }

        foreach (var specification in specifications)
        {
            writeStore.Remove(specification);
        }

        foreach (var variant in variants)
        {
            writeStore.Remove(variant);
        }

        writeStore.Remove(product);
        await writeStore.SaveChangesAsync(cancellationToken);

        return Result<AdminProductDto>.Success(dto);
    }

    private Task<Dictionary<Guid, Category>> GetCategoriesByIdAsync(CancellationToken cancellationToken)
    {
        return catalogStore.Categories.ToDictionaryAsyncSafe(category => category.Id, cancellationToken);
    }

    private Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return catalogStore.Categories
            .Where(category => category.Id == id)
            .AnyAsyncSafe(cancellationToken);
    }

    private Task<bool> ProductSlugExistsAsync(
        string slug,
        Guid? excludedProductId = null,
        CancellationToken cancellationToken = default)
    {
        return catalogStore.Products
            .Where(product =>
                product.Slug == slug &&
                (excludedProductId == null || product.Id != excludedProductId.Value))
            .AnyAsyncSafe(cancellationToken);
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
            category?.Name.Get("en"),
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

    private static string NormalizeSlug(string slug)
    {
        return slug.Trim().ToLowerInvariant();
    }
}
