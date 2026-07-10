using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Reviews;

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
    private readonly List<Coupon> _coupons = [];
    private readonly List<CouponProductTarget> _couponProductTargets = [];
    private readonly List<CouponRedemption> _couponRedemptions = [];
    private readonly List<CustomerLoyaltyAccount> _customerLoyaltyAccounts = [];
    private readonly List<LoyaltyTransaction> _loyaltyTransactions = [];
    private readonly List<LoyaltyTier> _loyaltyTiers = [];
    private readonly List<Order> _orders = [];
    private readonly List<OrderItem> _orderItems = [];
    private readonly List<OrderStatusHistory> _orderStatusHistories = [];
    private readonly List<BlogPost> _blogPosts = [];
    private readonly List<BlogPostRelatedProduct> _blogPostRelatedProducts = [];
    private readonly List<BlogComment> _blogComments = [];
    private readonly List<Review> _reviews = [];
    private readonly List<CustomerAddress> _customerAddresses = [];
    private readonly List<CustomerLoginHistory> _customerLoginHistories = [];

    public IQueryable<Category> Categories => _categories.AsQueryable();

    public IQueryable<Product> Products => _products.AsQueryable();

    public IQueryable<ProductVariant> ProductVariants => _productVariants.AsQueryable();

    public IQueryable<ProductImage> ProductImages => _productImages.AsQueryable();

    public IQueryable<ProductSpecification> ProductSpecifications => _productSpecifications.AsQueryable();

    public IQueryable<Banner> Banners => _banners.AsQueryable();

    public IQueryable<Customer> Customers => _customers.AsQueryable();

    public IQueryable<Coupon> Coupons => _coupons.AsQueryable();

    public IQueryable<CouponProductTarget> CouponProductTargets => _couponProductTargets.AsQueryable();

    public IQueryable<CouponRedemption> CouponRedemptions => _couponRedemptions.AsQueryable();

    public IQueryable<CustomerLoyaltyAccount> CustomerLoyaltyAccounts => _customerLoyaltyAccounts.AsQueryable();

    public IQueryable<LoyaltyTransaction> LoyaltyTransactions => _loyaltyTransactions.AsQueryable();

    public IQueryable<LoyaltyTier> LoyaltyTiers => _loyaltyTiers.AsQueryable();

    public IQueryable<Order> Orders => _orders.AsQueryable();

    public IQueryable<OrderItem> OrderItems => _orderItems.AsQueryable();

    public IQueryable<OrderStatusHistory> OrderStatusHistories => _orderStatusHistories.AsQueryable();

    public IQueryable<BlogPost> BlogPosts => _blogPosts.AsQueryable();

    public IQueryable<BlogPostRelatedProduct> BlogPostRelatedProducts => _blogPostRelatedProducts.AsQueryable();

    public IQueryable<BlogComment> BlogComments => _blogComments.AsQueryable();

    public IQueryable<Review> Reviews => _reviews.AsQueryable();

    public IQueryable<CustomerAddress> CustomerAddresses => _customerAddresses.AsQueryable();

    public IQueryable<CustomerLoginHistory> CustomerLoginHistories => _customerLoginHistories.AsQueryable();

    public int SaveChangesCallCount { get; private set; }

    public int TransactionCallCount { get; private set; }

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

    public void Seed(params Coupon[] coupons)
    {
        _coupons.AddRange(coupons);
        _couponProductTargets.AddRange(coupons.SelectMany(coupon => coupon.ProductTargets));
    }

    public void Seed(params CouponProductTarget[] couponProductTargets)
    {
        _couponProductTargets.AddRange(couponProductTargets);
    }

    public void Seed(params CouponRedemption[] couponRedemptions)
    {
        _couponRedemptions.AddRange(couponRedemptions);
    }

    public void Seed(params CustomerLoyaltyAccount[] customerLoyaltyAccounts)
    {
        _customerLoyaltyAccounts.AddRange(customerLoyaltyAccounts);
        _loyaltyTransactions.AddRange(customerLoyaltyAccounts.SelectMany(account => account.Transactions));
    }

    public void Seed(params LoyaltyTransaction[] loyaltyTransactions)
    {
        _loyaltyTransactions.AddRange(loyaltyTransactions);
    }

    public void Seed(params LoyaltyTier[] loyaltyTiers)
    {
        _loyaltyTiers.AddRange(loyaltyTiers);
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

    public void Seed(params BlogPost[] blogPosts)
    {
        _blogPosts.AddRange(blogPosts);
    }

    public void Seed(params BlogPostRelatedProduct[] relatedProducts)
    {
        _blogPostRelatedProducts.AddRange(relatedProducts);
    }

    public void Seed(params BlogComment[] comments)
    {
        _blogComments.AddRange(comments);
    }

    public void Seed(params Review[] reviews)
    {
        _reviews.AddRange(reviews);
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

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TransactionCallCount++;

        await operation(cancellationToken);
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

        if (typeof(TEntity) == typeof(CustomerLoyaltyAccount))
        {
            return (List<TEntity>)(object)_customerLoyaltyAccounts;
        }

        if (typeof(TEntity) == typeof(LoyaltyTransaction))
        {
            return (List<TEntity>)(object)_loyaltyTransactions;
        }

        if (typeof(TEntity) == typeof(LoyaltyTier))
        {
            return (List<TEntity>)(object)_loyaltyTiers;
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

        if (typeof(TEntity) == typeof(BlogPost))
        {
            return (List<TEntity>)(object)_blogPosts;
        }

        if (typeof(TEntity) == typeof(BlogPostRelatedProduct))
        {
            return (List<TEntity>)(object)_blogPostRelatedProducts;
        }

        if (typeof(TEntity) == typeof(BlogComment))
        {
            return (List<TEntity>)(object)_blogComments;
        }

        if (typeof(TEntity) == typeof(Review))
        {
            return (List<TEntity>)(object)_reviews;
        }

        if (typeof(TEntity) == typeof(CustomerAddress))
        {
            return (List<TEntity>)(object)_customerAddresses;
        }

        if (typeof(TEntity) == typeof(CustomerLoginHistory))
        {
            return (List<TEntity>)(object)_customerLoginHistories;
        }

        throw new InvalidOperationException($"Unsupported entity type '{typeof(TEntity).Name}'.");
    }
}
