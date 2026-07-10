using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Tests.Common.Fakes;

internal sealed class FakeCheckoutStore : ICheckoutStore
{
    private readonly List<Cart> _carts = [];
    private readonly List<Category> _categories = [];
    private readonly List<Product> _products = [];
    private readonly List<ProductVariant> _productVariants = [];
    private readonly List<Coupon> _coupons = [];
    private readonly List<CouponProductTarget> _couponProductTargets = [];
    private readonly List<CouponRedemption> _couponRedemptions = [];
    private readonly List<Order> _orders = [];
    private readonly List<PaymentTransaction> _paymentTransactions = [];

    public IReadOnlyCollection<Cart> Carts => _carts;

    public IReadOnlyCollection<Order> Orders => _orders;

    public IReadOnlyCollection<PaymentTransaction> PaymentTransactions => _paymentTransactions;

    public IReadOnlyCollection<Coupon> Coupons => _coupons;

    public IReadOnlyCollection<CouponProductTarget> CouponProductTargets => _couponProductTargets;

    public IReadOnlyCollection<CouponRedemption> CouponRedemptions => _couponRedemptions;

    public int SaveChangesCallCount { get; private set; }

    public int TransactionCallCount { get; private set; }

    public Task<Cart?> FindCartBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_carts.FirstOrDefault(cart => cart.SessionId == sessionId));
    }

    public Task<ProductVariant?> FindProductVariantByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_productVariants.FirstOrDefault(variant => variant.Id == id));
    }

    public Task<Product?> FindProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_products.FirstOrDefault(product => product.Id == id));
    }

    public Task<Category?> FindCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_categories.FirstOrDefault(category => category.Id == id));
    }

    public Task<Coupon?> FindCouponByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_coupons.FirstOrDefault(coupon => coupon.Code == code));
    }

    public Task<Coupon?> FindCouponByCodeForUpdateAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_coupons.FirstOrDefault(coupon => coupon.Code == code));
    }

    public Task<IReadOnlyCollection<Guid>> FindCouponProductTargetIdsAsync(
        Guid couponId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<Guid> productIds = _couponProductTargets
            .Where(target => target.CouponId == couponId)
            .Select(target => target.ProductId)
            .ToArray();

        return Task.FromResult(productIds);
    }

    public Task<bool> OrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_orders.Any(order => order.OrderCode == orderCode));
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TransactionCallCount++;

        await operation(cancellationToken);
    }

    public void Seed(params Cart[] carts)
    {
        _carts.AddRange(carts);
    }

    public void Seed(params Category[] categories)
    {
        _categories.AddRange(categories);
    }

    public void Seed(params Product[] products)
    {
        _products.AddRange(products);
    }

    public void Seed(params ProductVariant[] variants)
    {
        _productVariants.AddRange(variants);
    }

    public void Seed(params Coupon[] coupons)
    {
        _coupons.AddRange(coupons);
    }

    public void Seed(params CouponProductTarget[] couponProductTargets)
    {
        _couponProductTargets.AddRange(couponProductTargets);
    }

    public void Seed(params CouponRedemption[] couponRedemptions)
    {
        _couponRedemptions.AddRange(couponRedemptions);
    }

    public void Seed(params Order[] orders)
    {
        _orders.AddRange(orders);
    }

    public void Seed(params PaymentTransaction[] paymentTransactions)
    {
        _paymentTransactions.AddRange(paymentTransactions);
    }

    public void Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        GetSet<TEntity>().Add(entity);
    }

    public void Update<TEntity>(TEntity entity)
        where TEntity : class
    {
        var set = GetSet<TEntity>();
        if (!set.Contains(entity))
        {
            set.Add(entity);
        }
    }

    public void Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        GetSet<TEntity>().Remove(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveChangesCallCount++;

        return Task.FromResult(1);
    }

    private List<TEntity> GetSet<TEntity>()
        where TEntity : class
    {
        if (typeof(TEntity) == typeof(Cart))
        {
            return (List<TEntity>)(object)_carts;
        }

        if (typeof(TEntity) == typeof(Category))
        {
            return (List<TEntity>)(object)_categories;
        }

        if (typeof(TEntity) == typeof(Product))
        {
            return (List<TEntity>)(object)_products;
        }

        if (typeof(TEntity) == typeof(ProductVariant))
        {
            return (List<TEntity>)(object)_productVariants;
        }

        if (typeof(TEntity) == typeof(Coupon))
        {
            return (List<TEntity>)(object)_coupons;
        }

        if (typeof(TEntity) == typeof(CouponProductTarget))
        {
            return (List<TEntity>)(object)_couponProductTargets;
        }

        if (typeof(TEntity) == typeof(CouponRedemption))
        {
            return (List<TEntity>)(object)_couponRedemptions;
        }

        if (typeof(TEntity) == typeof(Order))
        {
            return (List<TEntity>)(object)_orders;
        }

        if (typeof(TEntity) == typeof(PaymentTransaction))
        {
            return (List<TEntity>)(object)_paymentTransactions;
        }

        throw new InvalidOperationException($"Unsupported entity type '{typeof(TEntity).Name}'.");
    }
}
