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
using WorkspaceEcommerce.Domain.Modules.Warranties;

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

    /// <summary>
    /// Holds the serialized-unit row lock during activation so retried or
    /// concurrent requests for the same identifier become deterministic.
    /// </summary>
    Task<SerializedProductUnit?> FindSerializedProductUnitForUpdateAsync(
        WarrantyIdentifierType identifierType,
        int identifierKeyVersion,
        string identifierFingerprint,
        CancellationToken cancellationToken = default);

    Task<SerializedProductUnit?> FindSerializedProductUnitByIdForUpdateAsync(
        Guid unitId,
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

    IQueryable<WarrantyPlan> WarrantyPlans { get; }

    IQueryable<WarrantyPlanCoverage> WarrantyPlanCoverages { get; }

    IQueryable<ProductVariantWarrantyPlan> ProductVariantWarrantyPlans { get; }

    IQueryable<WarrantyImportBatch> WarrantyImportBatches { get; }

    IQueryable<SerializedProductUnit> SerializedProductUnits { get; }

    IQueryable<WarrantyEntitlement> WarrantyEntitlements { get; }

    IQueryable<WarrantyCoverageSnapshot> WarrantyCoverageSnapshots { get; }

    IQueryable<WarrantyAuditEvent> WarrantyAuditEvents { get; }

}
