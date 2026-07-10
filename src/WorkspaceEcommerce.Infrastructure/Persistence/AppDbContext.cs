using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Reviews;

namespace WorkspaceEcommerce.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext, ICartStore, ICheckoutStore
{
    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();

    public DbSet<Banner> Banners => Set<Banner>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<CustomerLoginHistory> CustomerLoginHistories => Set<CustomerLoginHistory>();

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<CouponProductTarget> CouponProductTargets => Set<CouponProductTarget>();

    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    public DbSet<CustomerLoyaltyAccount> CustomerLoyaltyAccounts => Set<CustomerLoyaltyAccount>();

    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();

    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();

    public DbSet<BlogPostRelatedProduct> BlogPostRelatedProducts => Set<BlogPostRelatedProduct>();

    public DbSet<BlogComment> BlogComments => Set<BlogComment>();

    public DbSet<Review> Reviews => Set<Review>();

    IQueryable<Category> IAppDbContext.Categories => Categories;

    IQueryable<Product> IAppDbContext.Products => Products;

    IQueryable<ProductVariant> IAppDbContext.ProductVariants => ProductVariants;

    IQueryable<ProductImage> IAppDbContext.ProductImages => ProductImages;

    IQueryable<ProductSpecification> IAppDbContext.ProductSpecifications => ProductSpecifications;

    IQueryable<Banner> IAppDbContext.Banners => Banners;

    IQueryable<Customer> IAppDbContext.Customers => Customers;

    IQueryable<CustomerAddress> IAppDbContext.CustomerAddresses => CustomerAddresses;

    IQueryable<CustomerLoginHistory> IAppDbContext.CustomerLoginHistories => CustomerLoginHistories;

    IQueryable<Coupon> IAppDbContext.Coupons => Coupons;

    IQueryable<CouponProductTarget> IAppDbContext.CouponProductTargets => CouponProductTargets;

    IQueryable<CouponRedemption> IAppDbContext.CouponRedemptions => CouponRedemptions;

    IQueryable<CustomerLoyaltyAccount> IAppDbContext.CustomerLoyaltyAccounts => CustomerLoyaltyAccounts;

    IQueryable<LoyaltyTransaction> IAppDbContext.LoyaltyTransactions => LoyaltyTransactions;

    IQueryable<LoyaltyTier> IAppDbContext.LoyaltyTiers => LoyaltyTiers;

    IQueryable<Order> IAppDbContext.Orders => Orders;

    IQueryable<OrderItem> IAppDbContext.OrderItems => OrderItems;

    IQueryable<OrderStatusHistory> IAppDbContext.OrderStatusHistories => OrderStatusHistories;

    IQueryable<BlogPost> IAppDbContext.BlogPosts => BlogPosts;

    IQueryable<BlogPostRelatedProduct> IAppDbContext.BlogPostRelatedProducts => BlogPostRelatedProducts;

    IQueryable<BlogComment> IAppDbContext.BlogComments => BlogComments;

    IQueryable<Review> IAppDbContext.Reviews => Reviews;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConcurrencyException("A concurrency conflict occurred while saving changes.", exception);
        }
    }

    async Task<Cart?> ICartStore.FindCartBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await Carts
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.SessionId == sessionId, cancellationToken);
    }

    async Task<ProductVariant?> ICartStore.FindProductVariantByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ProductVariants
            .FirstOrDefaultAsync(variant => variant.Id == id, cancellationToken);
    }

    async Task<Product?> ICartStore.FindProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await Products
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    async Task<ProductImage?> ICartStore.FindPrimaryProductImageByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return await ProductImages
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.ImageUrl)
            .FirstOrDefaultAsync(image => image.ProductId == productId, cancellationToken);
    }

    async Task<Category?> ICartStore.FindCategoryByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await Categories
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    async Task<Cart?> ICheckoutStore.FindCartBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await Carts
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.SessionId == sessionId, cancellationToken);
    }

    async Task<ProductVariant?> ICheckoutStore.FindProductVariantByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ProductVariants
            .FromSqlInterpolated($"SELECT * FROM catalog.product_variants WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    async Task<Product?> ICheckoutStore.FindProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await Products
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    async Task<Category?> ICheckoutStore.FindCategoryByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await Categories
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    async Task<Coupon?> ICheckoutStore.FindCouponByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return await Coupons
            .FirstOrDefaultAsync(coupon => coupon.Code == code, cancellationToken);
    }

    async Task<Coupon?> ICheckoutStore.FindCouponByCodeForUpdateAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return await Coupons
            .FromSqlInterpolated($"SELECT * FROM promotions.coupons WHERE code = {code} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    async Task<IReadOnlyCollection<Guid>> ICheckoutStore.FindCouponProductTargetIdsAsync(
        Guid couponId,
        CancellationToken cancellationToken)
    {
        return await CouponProductTargets
            .Where(target => target.CouponId == couponId)
            .Select(target => target.ProductId)
            .ToArrayAsync(cancellationToken);
    }

    async Task<bool> ICheckoutStore.OrderCodeExistsAsync(
        string orderCode,
        CancellationToken cancellationToken)
    {
        return await Orders.AnyAsync(order => order.OrderCode == orderCode, cancellationToken);
    }

    async Task ICheckoutStore.ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionCoreAsync(operation, cancellationToken);
    }

    async Task IAppDbContext.ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionCoreAsync(operation, cancellationToken);
    }

    private async Task ExecuteInTransactionCoreAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    void IAppDbContext.Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Add(entity);
    }

    void IAppDbContext.Update<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Update(entity);
    }

    void IAppDbContext.Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Remove(entity);
    }

    void ICartStore.Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Add(entity);
    }

    void ICartStore.Update<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Update(entity);
    }

    void ICartStore.Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Remove(entity);
    }

    void ICheckoutStore.Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Add(entity);
    }

    void ICheckoutStore.Update<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Update(entity);
    }

    void ICheckoutStore.Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Remove(entity);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
