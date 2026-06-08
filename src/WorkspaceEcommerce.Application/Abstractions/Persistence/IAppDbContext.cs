using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    IQueryable<Category> Categories { get; }

    IQueryable<Product> Products { get; }

    IQueryable<ProductVariant> ProductVariants { get; }

    IQueryable<ProductImage> ProductImages { get; }

    IQueryable<ProductSpecification> ProductSpecifications { get; }

    IQueryable<Order> Orders { get; }

    IQueryable<OrderItem> OrderItems { get; }

    IQueryable<OrderStatusHistory> OrderStatusHistories { get; }

    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    void Update<TEntity>(TEntity entity)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
