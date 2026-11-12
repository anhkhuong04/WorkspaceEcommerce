using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface ICatalogReadStore
{
    IQueryable<StockAdjustment> StockAdjustments => Array.Empty<StockAdjustment>().AsQueryable();
    IQueryable<Category> Categories { get; }

    IQueryable<Category> SearchCategories(string search) => Categories.Where(category =>
        category.Slug.ToLower().Contains(search) || category.Name.Values.Any(name => name.ToLower().Contains(search)));

    IQueryable<Product> Products { get; }

    // Providers implement this against their native localized JSON representation.
    IQueryable<Product> SearchProducts(string search) => Products
        .Where(product => product.Slug.Contains(search) || product.Name.Values.Any(name => name.ToLower().Contains(search)));

    IQueryable<ProductVariant> ProductVariants { get; }

    Task<ProductVariant?> FindVariantForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(ProductVariants.FirstOrDefault(variant => variant.Id == id));

    IQueryable<ProductImage> ProductImages { get; }

    IQueryable<ProductSpecification> ProductSpecifications { get; }
}
