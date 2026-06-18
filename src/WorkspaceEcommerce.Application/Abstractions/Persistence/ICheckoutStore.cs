using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface ICheckoutStore
{
    Task<Cart?> FindCartBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<ProductVariant?> FindProductVariantByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Product?> FindProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Category?> FindCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Coupon?> FindCouponByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<Coupon?> FindCouponByCodeForUpdateAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> FindCouponProductTargetIdsAsync(
        Guid couponId,
        CancellationToken cancellationToken = default);

    Task<bool> OrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    void Update<TEntity>(TEntity entity)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
