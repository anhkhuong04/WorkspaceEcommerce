using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

internal sealed class StorefrontCatalogService(IAppDbContext dbContext) : IStorefrontCatalogService
{
    public Task<Result<IReadOnlyCollection<StorefrontCategoryDto>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var categories = dbContext.Categories
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToArray();

        return Task.FromResult(
            Result<IReadOnlyCollection<StorefrontCategoryDto>>.Success(BuildCategoryTree(categories)));
    }

    public Task<Result<PagedResult<StorefrontProductListItemDto>>> GetProductsAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCategorySlug = NormalizeOptional(request.CategorySlug);
        var normalizedSearch = NormalizeOptional(request.Search);
        var activeCategoriesById = dbContext.Categories
            .Where(category => category.IsActive)
            .ToDictionary(category => category.Id);
        var activeVariantsByProductId = dbContext.ProductVariants
            .Where(variant => variant.IsActive)
            .ToLookup(variant => variant.ProductId);
        var imagesByProductId = dbContext.ProductImages
            .OrderBy(image => image.SortOrder)
            .ToLookup(image => image.ProductId);

        var products = dbContext.Products
            .Where(product => product.IsActive)
            .ToArray()
            .Where(product => activeCategoriesById.ContainsKey(product.CategoryId))
            .Where(product => normalizedCategorySlug is null || activeCategoriesById[product.CategoryId].Slug == normalizedCategorySlug)
            .Where(product => normalizedSearch is null || product.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .Where(product => MatchesPriceFilter(activeVariantsByProductId[product.Id], request.MinPrice, request.MaxPrice))
            .Where(product => MatchesStockFilter(activeVariantsByProductId[product.Id], request.InStock));

        var sortedProducts = ApplyProductSorting(products, request.SortBy, activeVariantsByProductId)
            .ToArray();

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var items = sortedProducts
            .Skip(request.Skip)
            .Take(pageSize)
            .Select(product => ToListItemDto(
                product,
                activeCategoriesById[product.CategoryId],
                activeVariantsByProductId[product.Id],
                imagesByProductId[product.Id].FirstOrDefault()))
            .ToArray();

        var page = new PagedResult<StorefrontProductListItemDto>(
            items,
            pageNumber,
            pageSize,
            sortedProducts.Length);

        return Task.FromResult(Result<PagedResult<StorefrontProductListItemDto>>.Success(page));
    }

    public Task<Result<StorefrontProductDetailDto>> GetProductBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSlug = NormalizeRequired(slug);
        var product = dbContext.Products.FirstOrDefault(existing =>
            existing.IsActive && existing.Slug == normalizedSlug);

        if (product is null)
        {
            return Task.FromResult(Result<StorefrontProductDetailDto>.NotFound("Product was not found."));
        }

        var category = dbContext.Categories.FirstOrDefault(existing =>
            existing.Id == product.CategoryId && existing.IsActive);

        if (category is null)
        {
            return Task.FromResult(Result<StorefrontProductDetailDto>.NotFound("Product was not found."));
        }

        var variants = dbContext.ProductVariants
            .Where(variant => variant.ProductId == product.Id && variant.IsActive)
            .OrderBy(variant => variant.Sku)
            .ToArray();
        var images = dbContext.ProductImages
            .Where(image => image.ProductId == product.Id)
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.ImageUrl)
            .ToArray();
        var specifications = dbContext.ProductSpecifications
            .Where(specification => specification.ProductId == product.Id)
            .OrderBy(specification => specification.SortOrder)
            .ThenBy(specification => specification.Name)
            .ToArray();

        return Task.FromResult(Result<StorefrontProductDetailDto>.Success(
            ToDetailDto(product, category, variants, images, specifications)));
    }

    private static IReadOnlyCollection<StorefrontCategoryDto> BuildCategoryTree(IReadOnlyCollection<Category> categories)
    {
        return categories
            .Where(category => category.ParentId is null || categories.All(parent => parent.Id != category.ParentId.Value))
            .Select(category => ToCategoryDto(category, BuildChildren(category.Id, categories)))
            .ToArray();
    }

    private static IReadOnlyCollection<StorefrontCategoryDto> BuildChildren(
        Guid parentId,
        IReadOnlyCollection<Category> categories)
    {
        return categories
            .Where(category => category.ParentId == parentId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => ToCategoryDto(category, BuildChildren(category.Id, categories)))
            .ToArray();
    }

    private static StorefrontCategoryDto ToCategoryDto(
        Category category,
        IReadOnlyCollection<StorefrontCategoryDto> children)
    {
        return new StorefrontCategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Slug,
            category.SortOrder,
            children);
    }

    private static StorefrontProductListItemDto ToListItemDto(
        Product product,
        Category category,
        IEnumerable<ProductVariant> activeVariants,
        ProductImage? primaryImage)
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
            category.Name,
            product.Name,
            product.Slug,
            product.Description,
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
        IEnumerable<ProductSpecification> specifications)
    {
        return new StorefrontProductDetailDto(
            product.Id,
            product.CategoryId,
            category.Name,
            product.Name,
            product.Slug,
            product.Description,
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

    private static bool MatchesPriceFilter(
        IEnumerable<ProductVariant> activeVariants,
        decimal? minPrice,
        decimal? maxPrice)
    {
        var variants = activeVariants.ToArray();
        if (minPrice is null && maxPrice is null)
        {
            return true;
        }

        return variants.Any(variant =>
            (minPrice is null || variant.Price >= minPrice.Value) &&
            (maxPrice is null || variant.Price <= maxPrice.Value));
    }

    private static bool MatchesStockFilter(IEnumerable<ProductVariant> activeVariants, bool? inStock)
    {
        if (inStock is null)
        {
            return true;
        }

        return inStock.Value
            ? activeVariants.Any(variant => variant.StockQuantity > 0)
            : activeVariants.All(variant => variant.StockQuantity <= 0);
    }

    private static IEnumerable<Product> ApplyProductSorting(
        IEnumerable<Product> products,
        string? sortBy,
        ILookup<Guid, ProductVariant> activeVariantsByProductId)
    {
        var normalizedSortBy = NormalizeOptional(sortBy) ?? "name-asc";

        return normalizedSortBy switch
        {
            "price-asc" => products
                .OrderBy(product => GetMinPrice(activeVariantsByProductId[product.Id]) is null)
                .ThenBy(product => GetMinPrice(activeVariantsByProductId[product.Id]))
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Slug),
            "price-desc" => products
                .OrderBy(product => GetMinPrice(activeVariantsByProductId[product.Id]) is null)
                .ThenByDescending(product => GetMinPrice(activeVariantsByProductId[product.Id]))
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Slug),
            "updated-desc" => products
                .OrderByDescending(product => product.UpdatedAt)
                .ThenBy(product => product.Name)
                .ThenBy(product => product.Slug),
            _ => products
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Slug)
        };
    }

    private static decimal? GetMinPrice(IEnumerable<ProductVariant> activeVariants)
    {
        var variants = activeVariants.ToArray();
        return variants.Length == 0
            ? null
            : variants.Min(variant => variant.Price);
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
}
