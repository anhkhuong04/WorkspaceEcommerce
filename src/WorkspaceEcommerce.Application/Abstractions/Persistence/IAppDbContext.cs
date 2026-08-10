using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Reviews;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IAppDbContext : ICatalogReadStore, IOrderReadStore, ILoyaltyReadStore, IAppWriteStore
{
    IQueryable<Banner> Banners { get; }

    IQueryable<Customer> Customers { get; }

    IQueryable<CustomerAddress> CustomerAddresses { get; }

    IQueryable<CustomerLoginHistory> CustomerLoginHistories { get; }

    IQueryable<CustomerTwoFactorChallenge> CustomerTwoFactorChallenges { get; }

    IQueryable<CustomerTwoFactorRecoveryCode> CustomerTwoFactorRecoveryCodes { get; }

    IQueryable<CustomerAccountToken> CustomerAccountTokens { get; }

    IQueryable<CustomerRefreshTokenFamily> CustomerRefreshTokenFamilies { get; }

    IQueryable<CustomerRefreshToken> CustomerRefreshTokens { get; }

    IQueryable<CustomerEmailOutboxMessage> CustomerEmailOutboxMessages { get; }

    Task<CustomerRefreshToken?> FindCustomerRefreshTokenByHashForUpdateAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a VNPay transaction while holding its PostgreSQL row lock inside
    /// the caller's transaction. Callback retries from VNPay can otherwise
    /// race each other before the terminal state is persisted.
    /// </summary>
    Task<PaymentTransaction?> FindVNPayPaymentTransactionForUpdateAsync(
        string txnRef,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an order while holding its PostgreSQL row lock inside the caller's
    /// transaction.
    /// </summary>
    Task<Order?> FindOrderForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ShipmentCommandOutbox[]> ClaimDueShipmentCommandsAsync(
        string leaseOwner,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<bool> CompleteShipmentCommandLeaseAsync(
        Guid commandId,
        Guid leaseToken,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> RetryShipmentCommandLeaseAsync(
        Guid commandId,
        Guid leaseToken,
        string error,
        string errorCategory,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> DeadLetterShipmentCommandLeaseAsync(
        Guid commandId,
        Guid leaseToken,
        string error,
        string errorCategory,
        DateTimeOffset deadLetteredAtUtc,
        CancellationToken cancellationToken = default);

    IQueryable<Coupon> Coupons { get; }

    IQueryable<CouponProductTarget> CouponProductTargets { get; }

    IQueryable<CouponRedemption> CouponRedemptions { get; }

    IQueryable<PaymentTransaction> PaymentTransactions { get; }

    IQueryable<BlogPost> BlogPosts { get; }

    IQueryable<BlogPostRelatedProduct> BlogPostRelatedProducts { get; }

    IQueryable<BlogComment> BlogComments { get; }

    IQueryable<Review> Reviews { get; }

}
