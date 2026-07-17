using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface ICatalogReadStore
{
    IQueryable<Category> Categories { get; }

    IQueryable<Product> Products { get; }

    IQueryable<ProductVariant> ProductVariants { get; }

    IQueryable<ProductImage> ProductImages { get; }

    IQueryable<ProductSpecification> ProductSpecifications { get; }
}
