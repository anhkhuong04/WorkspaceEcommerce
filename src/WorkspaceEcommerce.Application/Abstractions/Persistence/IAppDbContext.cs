using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Reviews;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IAppDbContext : ICatalogReadStore, IOrderReadStore, ILoyaltyReadStore, IAppWriteStore
{
    IQueryable<Banner> Banners { get; }

    IQueryable<Customer> Customers { get; }

    IQueryable<CustomerAddress> CustomerAddresses { get; }

    IQueryable<CustomerLoginHistory> CustomerLoginHistories { get; }

    IQueryable<Coupon> Coupons { get; }

    IQueryable<CouponProductTarget> CouponProductTargets { get; }

    IQueryable<CouponRedemption> CouponRedemptions { get; }

    IQueryable<PaymentTransaction> PaymentTransactions { get; }

    IQueryable<BlogPost> BlogPosts { get; }

    IQueryable<BlogPostRelatedProduct> BlogPostRelatedProducts { get; }

    IQueryable<BlogComment> BlogComments { get; }

    IQueryable<Review> Reviews { get; }

}
