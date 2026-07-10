using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Reviews;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    IQueryable<Category> Categories { get; }

    IQueryable<Product> Products { get; }

    IQueryable<ProductVariant> ProductVariants { get; }

    IQueryable<ProductImage> ProductImages { get; }

    IQueryable<ProductSpecification> ProductSpecifications { get; }

    IQueryable<Banner> Banners { get; }

    IQueryable<Customer> Customers { get; }

    IQueryable<CustomerAddress> CustomerAddresses { get; }

    IQueryable<CustomerLoginHistory> CustomerLoginHistories { get; }

    IQueryable<Coupon> Coupons { get; }

    IQueryable<CouponProductTarget> CouponProductTargets { get; }

    IQueryable<CouponRedemption> CouponRedemptions { get; }

    IQueryable<CustomerLoyaltyAccount> CustomerLoyaltyAccounts { get; }

    IQueryable<LoyaltyTransaction> LoyaltyTransactions { get; }

    IQueryable<LoyaltyTier> LoyaltyTiers { get; }

    IQueryable<Order> Orders { get; }

    IQueryable<OrderItem> OrderItems { get; }

    IQueryable<OrderStatusHistory> OrderStatusHistories { get; }

    IQueryable<BlogPost> BlogPosts { get; }

    IQueryable<BlogPostRelatedProduct> BlogPostRelatedProducts { get; }

    IQueryable<BlogComment> BlogComments { get; }

    IQueryable<Review> Reviews { get; }

    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    void Update<TEntity>(TEntity entity)
        where TEntity : class;

    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
