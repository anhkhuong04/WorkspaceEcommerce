using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface ICartStore
{
    Task<Cart?> FindCartBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<ProductVariant?> FindProductVariantByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Product?> FindProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductImage?> FindPrimaryProductImageByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<Category?> FindCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    void Update<TEntity>(TEntity entity)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
