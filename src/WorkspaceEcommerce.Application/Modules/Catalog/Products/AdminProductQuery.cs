using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

internal static class AdminProductQuery
{
    public static IQueryable<Product> Filter(ICatalogReadStore store, AdminProductListRequest request)
    {
        var products = store.Products;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            var names = store.SearchProducts(search).Select(p => p.Id);
            products = products.Where(p => names.Contains(p.Id) || store.ProductVariants.Any(v => v.ProductId == p.Id && v.Sku.ToLower().Contains(search)));
        }
        if (request.ProductId.HasValue) products = products.Where(p => p.Id == request.ProductId);
        if (request.Ids is not null)
        {
            var ids = request.Ids.Split(',').Select(Guid.Parse).Distinct().ToArray();
            products = products.Where(p => ids.Contains(p.Id));
        }
        if (request.CategoryId.HasValue) products = products.Where(p => p.CategoryId == request.CategoryId);
        if (request.IsActive.HasValue) products = products.Where(p => p.IsActive == request.IsActive);
        if (request.IsFeatured.HasValue) products = products.Where(p => p.IsFeatured == request.IsFeatured);
        if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
            products = products.Where(p => store.ProductVariants.Any(v => v.ProductId == p.Id && v.IsActive &&
                (!request.MinPrice.HasValue || v.Price >= request.MinPrice) && (!request.MaxPrice.HasValue || v.Price <= request.MaxPrice)));
        products = request.Stock switch
        {
            "in" => products.Where(p => store.ProductVariants.Any(v => v.ProductId == p.Id && v.IsActive && v.StockQuantity > 0)),
            "out" => products.Where(p => !store.ProductVariants.Any(v => v.ProductId == p.Id && v.IsActive && v.StockQuantity > 0)),
            "low" => products.Where(p => store.ProductVariants.Any(v => v.ProductId == p.Id && v.IsActive && v.StockQuantity <= 5)),
            _ => products
        };
        return products;
    }

    public static IOrderedQueryable<Product> Sort(ICatalogReadStore store, IQueryable<Product> products, string sort) => sort switch
    {
        "created-desc" => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
        "price-asc" => products.OrderBy(p => store.ProductVariants.Where(v => v.ProductId == p.Id && v.IsActive).Select(v => (decimal?)v.Price).Min() ?? decimal.MaxValue).ThenBy(p => p.Id),
        "price-desc" => products.OrderByDescending(p => store.ProductVariants.Where(v => v.ProductId == p.Id && v.IsActive).Select(v => (decimal?)v.Price).Min() ?? 0).ThenBy(p => p.Id),
        "stock-asc" => products.OrderBy(p => store.ProductVariants.Where(v => v.ProductId == p.Id && v.IsActive).Sum(v => (int?)v.StockQuantity) ?? 0).ThenBy(p => p.Id),
        _ => products.OrderBy(p => p.Slug).ThenBy(p => p.Id)
    };
}
