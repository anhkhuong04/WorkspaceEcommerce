using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Tests.Common.Fakes;

internal sealed class FakeCartStore : ICartStore
{
    private readonly List<Cart> _carts = [];
    private readonly List<CartItem> _cartItems = [];
    private readonly List<Category> _categories = [];
    private readonly List<Product> _products = [];
    private readonly List<ProductVariant> _productVariants = [];

    public int SaveChangesCallCount { get; private set; }

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

        if (typeof(TEntity) == typeof(CartItem))
        {
            return (List<TEntity>)(object)_cartItems;
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

        throw new InvalidOperationException($"Unsupported entity type '{typeof(TEntity).Name}'.");
    }
}
