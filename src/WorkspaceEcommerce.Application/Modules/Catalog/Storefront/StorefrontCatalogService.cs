using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

internal sealed class StorefrontCatalogService(ICatalogReadStore catalogStore, ICurrentLanguageProvider languageProvider) : IStorefrontCatalogService
{
    public async Task<Result<IReadOnlyCollection<StorefrontCategoryDto>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await catalogStore.Categories
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Slug)
            .ToArrayAsyncSafe(cancellationToken);

        return Result<IReadOnlyCollection<StorefrontCategoryDto>>.Success(
            BuildCategoryTree(categories, languageProvider.CurrentLanguage));
    }

    public async Task<Result<PagedResult<StorefrontProductListItemDto>>> GetProductsAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentLanguage = languageProvider.CurrentLanguage;
        var normalizedCategorySlug = NormalizeOptional(request.CategorySlug);
        var normalizedSearch = NormalizeOptional(request.Search);
        var query =
            from product in catalogStore.Products
            join category in catalogStore.Categories on product.CategoryId equals category.Id
            where product.IsActive && category.IsActive
            select new ProductCatalogRow(product, category);

        if (normalizedCategorySlug is not null)
        {
            query = query.Where(row => row.Category.Slug == normalizedCategorySlug);
        }

        if (normalizedSearch is not null)
        {
            query = query.Where(row =>
                row.Product.Slug.Contains(normalizedSearch) ||
                (row.Product.Name.ContainsKey(currentLanguage) && row.Product.Name[currentLanguage].ToLower().Contains(normalizedSearch)) ||
                (row.Product.Name.ContainsKey("en") && row.Product.Name["en"].ToLower().Contains(normalizedSearch)));
        }

        if (request.MinPrice is not null || request.MaxPrice is not null)
        {
            query = query.Where(row => catalogStore.ProductVariants.Any(variant =>
                variant.ProductId == row.Product.Id &&
                variant.IsActive &&
                (request.MinPrice == null || variant.Price >= request.MinPrice.Value) &&
                (request.MaxPrice == null || variant.Price <= request.MaxPrice.Value)));
        }

        if (request.InStock is not null)
        {
            query = request.InStock.Value
                ? query.Where(row => catalogStore.ProductVariants.Any(variant =>
                    variant.ProductId == row.Product.Id &&
                    variant.IsActive &&
                    variant.StockQuantity > 0))
                : query.Where(row => !catalogStore.ProductVariants.Any(variant =>
                    variant.ProductId == row.Product.Id &&
                    variant.IsActive &&
                    variant.StockQuantity > 0));
        }

        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var rows = await ApplyProductSorting(query, request.SortBy, currentLanguage)
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .ToArrayAsyncSafe(cancellationToken);
        var productIds = rows.Select(row => row.Product.Id).ToArray();
        var activeVariantsByProductId = (await catalogStore.ProductVariants
            .Where(variant => productIds.Contains(variant.ProductId) && variant.IsActive)
            .OrderBy(variant => variant.Sku)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(variant => variant.ProductId);
        var imagesByProductId = (await catalogStore.ProductImages
            .Where(image => productIds.Contains(image.ProductId))
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.ImageUrl)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(image => image.ProductId);
        var items = rows
            .Select(row => ToListItemDto(
                row.Product,
                row.Category,
                activeVariantsByProductId[row.Product.Id],
                imagesByProductId[row.Product.Id].FirstOrDefault(),
                currentLanguage))
            .ToArray();

        return Result<PagedResult<StorefrontProductListItemDto>>.Success(
            new PagedResult<StorefrontProductListItemDto>(
                items,
                request.NormalizedPageNumber,
                request.NormalizedPageSize,
                totalCount));
    }

    public async Task<Result<StorefrontProductDetailDto>> GetProductBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeRequired(slug);
        var product = await catalogStore.Products
            .Where(existing => existing.IsActive && existing.Slug == normalizedSlug)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        if (product is null)
        {
            return Result<StorefrontProductDetailDto>.NotFound("Product was not found.");
        }

        var category = await catalogStore.Categories
            .Where(existing => existing.Id == product.CategoryId && existing.IsActive)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        if (category is null)
        {
            return Result<StorefrontProductDetailDto>.NotFound("Product was not found.");
        }

        var variants = await catalogStore.ProductVariants
            .Where(variant => variant.ProductId == product.Id && variant.IsActive)
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

        return Result<StorefrontProductDetailDto>.Success(
            ToDetailDto(product, category, variants, images, specifications, languageProvider.CurrentLanguage));
    }

    private IQueryable<ProductCatalogRow> ApplyProductSorting(
        IQueryable<ProductCatalogRow> products,
        string? sortBy,
        string currentLanguage)
    {
        var normalizedSortBy = NormalizeOptional(sortBy) ?? "name-asc";

        return normalizedSortBy switch
        {
            "price-asc" => products
                .OrderBy(row => !catalogStore.ProductVariants.Any(variant => variant.ProductId == row.Product.Id && variant.IsActive))
                .ThenBy(row => catalogStore.ProductVariants
                    .Where(variant => variant.ProductId == row.Product.Id && variant.IsActive)
                    .Select(variant => (decimal?)variant.Price)
                    .Min())
                .ThenBy(row => row.Product.Name.ContainsKey(currentLanguage) ? row.Product.Name[currentLanguage] : row.Product.Slug)
                .ThenBy(row => row.Product.Slug),
            "price-desc" => products
                .OrderBy(row => !catalogStore.ProductVariants.Any(variant => variant.ProductId == row.Product.Id && variant.IsActive))
                .ThenByDescending(row => catalogStore.ProductVariants
                    .Where(variant => variant.ProductId == row.Product.Id && variant.IsActive)
                    .Select(variant => (decimal?)variant.Price)
                    .Min())
                .ThenBy(row => row.Product.Name.ContainsKey(currentLanguage) ? row.Product.Name[currentLanguage] : row.Product.Slug)
                .ThenBy(row => row.Product.Slug),
            "updated-desc" => products
                .OrderByDescending(row => row.Product.UpdatedAt)
                .ThenBy(row => row.Product.Name.ContainsKey(currentLanguage) ? row.Product.Name[currentLanguage] : row.Product.Slug)
                .ThenBy(row => row.Product.Slug),
            _ => products
                .OrderBy(row => row.Product.Name.ContainsKey(currentLanguage) ? row.Product.Name[currentLanguage] : row.Product.Slug)
                .ThenBy(row => row.Product.Slug)
        };
    }

    private static IReadOnlyCollection<StorefrontCategoryDto> BuildCategoryTree(IReadOnlyCollection<Category> categories, string currentLanguage)
    {
        return categories
            .Where(category => category.ParentId is null || categories.All(parent => parent.Id != category.ParentId.Value))
            .Select(category => ToCategoryDto(category, BuildChildren(category.Id, categories, currentLanguage), currentLanguage))
            .ToArray();
    }

    private static IReadOnlyCollection<StorefrontCategoryDto> BuildChildren(
        Guid parentId,
        IReadOnlyCollection<Category> categories,
        string currentLanguage)
    {
        return categories
            .Where(category => category.ParentId == parentId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name.Get(currentLanguage))
            .Select(category => ToCategoryDto(category, BuildChildren(category.Id, categories, currentLanguage), currentLanguage))
            .ToArray();
    }

    private static StorefrontCategoryDto ToCategoryDto(
        Category category,
        IReadOnlyCollection<StorefrontCategoryDto> children,
        string currentLanguage)
    {
        return new StorefrontCategoryDto(
            category.Id,
            category.ParentId,
            category.Name.Get(currentLanguage),
            category.Slug,
            category.SortOrder,
            children);
    }

    private static StorefrontProductListItemDto ToListItemDto(
        Product product,
        Category category,
        IEnumerable<ProductVariant> activeVariants,
        ProductImage? primaryImage,
        string currentLanguage)
    {
        var variants = activeVariants.ToArray();
        decimal? minPrice = variants.Length == 0
            ? null
            : variants.Min(variant => variant.Price);
        var compareAtPrices = variants
            .Where(variant => variant.CompareAtPrice is not null)
            .Select(variant => variant.CompareAtPrice)
            .ToArray();
        decimal? compareAtPrice = compareAtPrices.Length == 0
            ? null
            : compareAtPrices.Max();

        return new StorefrontProductListItemDto(
            product.Id,
            product.CategoryId,
            category.Name.Get(currentLanguage),
            product.Name.Get(currentLanguage),
            product.Slug,
            product.Description?.Get(currentLanguage),
            product.IsFeatured,
            minPrice,
            compareAtPrice,
            variants.Any(variant => variant.StockQuantity > 0),
            primaryImage?.ImageUrl);
    }

    private static StorefrontProductDetailDto ToDetailDto(
        Product product,
        Category category,
        IEnumerable<ProductVariant> activeVariants,
        IEnumerable<ProductImage> images,
        IEnumerable<ProductSpecification> specifications,
        string currentLanguage)
    {
        return new StorefrontProductDetailDto(
            product.Id,
            product.CategoryId,
            category.Name.Get(currentLanguage),
            product.Name.Get(currentLanguage),
            product.Slug,
            product.Description?.Get(currentLanguage),
            product.IsFeatured,
            activeVariants.Select(ToVariantDto).ToArray(),
            images.Select(ToImageDto).ToArray(),
            specifications.Select(ToSpecificationDto).ToArray());
    }

    private static StorefrontProductVariantDto ToVariantDto(ProductVariant variant)
    {
        return new StorefrontProductVariantDto(
            variant.Id,
            variant.Sku,
            variant.Name,
            variant.Color,
            variant.Size,
            variant.Price,
            variant.CompareAtPrice,
            variant.StockQuantity,
            variant.RequiresInstallation);
    }

    private static StorefrontProductImageDto ToImageDto(ProductImage image)
    {
        return new StorefrontProductImageDto(
            image.Id,
            image.ImageUrl,
            image.AltText,
            image.SortOrder);
    }

    private static StorefrontProductSpecificationDto ToSpecificationDto(ProductSpecification specification)
    {
        return new StorefrontProductSpecificationDto(
            specification.Id,
            specification.Name,
            specification.Value,
            specification.SortOrder);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeRequired(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private sealed record ProductCatalogRow(Product Product, Category Category);
}
