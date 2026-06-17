using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Common.Fakes;

internal sealed class FakeAppDbContext : IAppDbContext
{
    private readonly List<Category> _categories = [];
    private readonly List<Product> _products = [];
    private readonly List<ProductVariant> _productVariants = [];
    private readonly List<ProductImage> _productImages = [];
    private readonly List<ProductSpecification> _productSpecifications = [];
    private readonly List<Banner> _banners = [];
    private readonly List<Customer> _customers = [];
    private readonly List<Order> _orders = [];
    private readonly List<OrderItem> _orderItems = [];
    private readonly List<OrderStatusHistory> _orderStatusHistories = [];

    public IQueryable<Category> Categories => _categories.AsQueryable();

    public IQueryable<Product> Products => _products.AsQueryable();

    public IQueryable<ProductVariant> ProductVariants => _productVariants.AsQueryable();

    public IQueryable<ProductImage> ProductImages => _productImages.AsQueryable();

    public IQueryable<ProductSpecification> ProductSpecifications => _productSpecifications.AsQueryable();

    public IQueryable<Banner> Banners => _banners.AsQueryable();

    public IQueryable<Customer> Customers => _customers.AsQueryable();

    public IQueryable<Order> Orders => _orders.AsQueryable();

    public IQueryable<OrderItem> OrderItems => _orderItems.AsQueryable();

    public IQueryable<OrderStatusHistory> OrderStatusHistories => _orderStatusHistories.AsQueryable();

    public int SaveChangesCallCount { get; private set; }

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

    public void Seed(params ProductImage[] images)
    {
        _productImages.AddRange(images);
    }

    public void Seed(params ProductSpecification[] specifications)
    {
        _productSpecifications.AddRange(specifications);
    }

    public void Seed(params Banner[] banners)
    {
        _banners.AddRange(banners);
    }

    public void Seed(params Customer[] customers)
    {
        _customers.AddRange(customers);
    }

    public void Seed(params Order[] orders)
    {
        _orders.AddRange(orders);
        _orderItems.AddRange(orders.SelectMany(order => order.Items));
        _orderStatusHistories.AddRange(orders.SelectMany(order => order.StatusHistory));
    }

    public void Seed(params OrderItem[] orderItems)
    {
        _orderItems.AddRange(orderItems);
    }

    public void Seed(params OrderStatusHistory[] orderStatusHistories)
    {
        _orderStatusHistories.AddRange(orderStatusHistories);
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

        if (typeof(TEntity) == typeof(ProductImage))
        {
            return (List<TEntity>)(object)_productImages;
        }

        if (typeof(TEntity) == typeof(ProductSpecification))
        {
            return (List<TEntity>)(object)_productSpecifications;
        }

        if (typeof(TEntity) == typeof(Banner))
        {
            return (List<TEntity>)(object)_banners;
        }

        if (typeof(TEntity) == typeof(Customer))
        {
            return (List<TEntity>)(object)_customers;
        }

        if (typeof(TEntity) == typeof(Order))
        {
            return (List<TEntity>)(object)_orders;
        }

        if (typeof(TEntity) == typeof(OrderItem))
        {
            return (List<TEntity>)(object)_orderItems;
        }

        if (typeof(TEntity) == typeof(OrderStatusHistory))
        {
            return (List<TEntity>)(object)_orderStatusHistories;
        }

        throw new InvalidOperationException($"Unsupported entity type '{typeof(TEntity).Name}'.");
    }
}
