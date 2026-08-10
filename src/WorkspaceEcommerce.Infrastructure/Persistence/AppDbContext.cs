using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Media;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Reviews;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext, ICartStore, ICheckoutStore
{
    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public DbSet<OrderShipment> OrderShipments => Set<OrderShipment>();

    public DbSet<ShipmentTimelineEntry> ShipmentTimelineEntries => Set<ShipmentTimelineEntry>();

    public DbSet<ShipmentEventInbox> ShipmentEventInbox => Set<ShipmentEventInbox>();

    public DbSet<ShipmentCommandOutbox> ShipmentCommandOutbox => Set<ShipmentCommandOutbox>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();

    public DbSet<Banner> Banners => Set<Banner>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<CustomerLoginHistory> CustomerLoginHistories => Set<CustomerLoginHistory>();

    public DbSet<CustomerTwoFactorChallenge> CustomerTwoFactorChallenges => Set<CustomerTwoFactorChallenge>();

    public DbSet<CustomerTwoFactorRecoveryCode> CustomerTwoFactorRecoveryCodes => Set<CustomerTwoFactorRecoveryCode>();

    public DbSet<CustomerAccountToken> CustomerAccountTokens => Set<CustomerAccountToken>();

    public DbSet<CustomerRefreshTokenFamily> CustomerRefreshTokenFamilies => Set<CustomerRefreshTokenFamily>();

    public DbSet<CustomerRefreshToken> CustomerRefreshTokens => Set<CustomerRefreshToken>();

    public DbSet<CustomerEmailOutboxMessage> CustomerEmailOutboxMessages => Set<CustomerEmailOutboxMessage>();

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<CouponProductTarget> CouponProductTargets => Set<CouponProductTarget>();

    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    public DbSet<CustomerLoyaltyAccount> CustomerLoyaltyAccounts => Set<CustomerLoyaltyAccount>();

    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();

    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();

    public DbSet<BlogPostRelatedProduct> BlogPostRelatedProducts => Set<BlogPostRelatedProduct>();

    public DbSet<BlogComment> BlogComments => Set<BlogComment>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MediaAssetVariant> MediaAssetVariants => Set<MediaAssetVariant>();

    public DbSet<Review> Reviews => Set<Review>();

    IQueryable<Category> ICatalogReadStore.Categories => Categories;

    IQueryable<Product> ICatalogReadStore.Products => Products;

    IQueryable<ProductVariant> ICatalogReadStore.ProductVariants => ProductVariants;

    IQueryable<ProductImage> ICatalogReadStore.ProductImages => ProductImages;

    IQueryable<ProductSpecification> ICatalogReadStore.ProductSpecifications => ProductSpecifications;

    IQueryable<Banner> IAppDbContext.Banners => Banners;

    IQueryable<Customer> IAppDbContext.Customers => Customers;

    IQueryable<CustomerAddress> IAppDbContext.CustomerAddresses => CustomerAddresses;

    IQueryable<CustomerLoginHistory> IAppDbContext.CustomerLoginHistories => CustomerLoginHistories;

    IQueryable<CustomerTwoFactorChallenge> IAppDbContext.CustomerTwoFactorChallenges => CustomerTwoFactorChallenges;

    IQueryable<CustomerTwoFactorRecoveryCode> IAppDbContext.CustomerTwoFactorRecoveryCodes => CustomerTwoFactorRecoveryCodes;

    IQueryable<CustomerAccountToken> IAppDbContext.CustomerAccountTokens => CustomerAccountTokens;

    IQueryable<CustomerRefreshTokenFamily> IAppDbContext.CustomerRefreshTokenFamilies => CustomerRefreshTokenFamilies;

    IQueryable<CustomerRefreshToken> IAppDbContext.CustomerRefreshTokens => CustomerRefreshTokens;

    IQueryable<CustomerEmailOutboxMessage> IAppDbContext.CustomerEmailOutboxMessages => CustomerEmailOutboxMessages;

    IQueryable<Coupon> IAppDbContext.Coupons => Coupons;

    IQueryable<CouponProductTarget> IAppDbContext.CouponProductTargets => CouponProductTargets;

    IQueryable<CouponRedemption> IAppDbContext.CouponRedemptions => CouponRedemptions;

    IQueryable<CustomerLoyaltyAccount> ILoyaltyReadStore.CustomerLoyaltyAccounts => CustomerLoyaltyAccounts;

    IQueryable<LoyaltyTransaction> ILoyaltyReadStore.LoyaltyTransactions => LoyaltyTransactions;

    IQueryable<LoyaltyTier> ILoyaltyReadStore.LoyaltyTiers => LoyaltyTiers;

    IQueryable<Order> IOrderReadStore.Orders => Orders;

    IQueryable<OrderItem> IOrderReadStore.OrderItems => OrderItems;

    IQueryable<OrderStatusHistory> IOrderReadStore.OrderStatusHistories => OrderStatusHistories;

    IQueryable<OrderShipment> IOrderReadStore.OrderShipments => OrderShipments;

    IQueryable<ShipmentTimelineEntry> IOrderReadStore.ShipmentTimelineEntries => ShipmentTimelineEntries;

    IQueryable<ShipmentEventInbox> IOrderReadStore.ShipmentEventInbox => ShipmentEventInbox;

    IQueryable<ShipmentCommandOutbox> IOrderReadStore.ShipmentCommandOutbox => ShipmentCommandOutbox;

    IQueryable<PaymentTransaction> IAppDbContext.PaymentTransactions => PaymentTransactions;

    IQueryable<BlogPost> IAppDbContext.BlogPosts => BlogPosts;

    IQueryable<BlogPostRelatedProduct> IAppDbContext.BlogPostRelatedProducts => BlogPostRelatedProducts;

    IQueryable<BlogComment> IAppDbContext.BlogComments => BlogComments;

    IQueryable<Review> IAppDbContext.Reviews => Reviews;

    Task<CustomerRefreshToken?> IAppDbContext.FindCustomerRefreshTokenByHashForUpdateAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        CustomerRefreshTokens
            .FromSqlInterpolated($"SELECT * FROM customer.refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    Task<PaymentTransaction?> IAppDbContext.FindVNPayPaymentTransactionForUpdateAsync(
        string txnRef,
        CancellationToken cancellationToken) =>
        PaymentTransactions
            .FromSqlInterpolated($"""
                SELECT *
                FROM payments.payment_transactions
                WHERE provider = 'VNPay' AND txn_ref = {txnRef}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(cancellationToken);

    Task<Order?> IAppDbContext.FindOrderForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken) =>
        Orders
            .FromSqlInterpolated($"SELECT * FROM ordering.orders WHERE id = {orderId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    async Task<ShipmentCommandOutbox[]> IAppDbContext.ClaimDueShipmentCommandsAsync(
        string leaseOwner,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new ArgumentException("A shipment command lease owner is required.", nameof(leaseOwner));
        }

        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Shipment command lease duration must be between zero and one hour.");
        }

        if (batchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Shipment command batch size must be between 1 and 100.");
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var databaseNow = await Database
            .SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        var leaseExpiresAt = databaseNow.Add(leaseDuration);
        var commands = await ShipmentCommandOutbox
            .FromSqlInterpolated($"""
                SELECT *
                FROM shipping.shipment_command_outbox
                WHERE status IN ('Pending', 'Leased')
                  AND completed_at_utc IS NULL
                  AND dead_lettered_at_utc IS NULL
                  AND next_attempt_at_utc <= clock_timestamp()
                  AND (lease_expires_at_utc IS NULL OR lease_expires_at_utc <= clock_timestamp())
                ORDER BY next_attempt_at_utc, created_at_utc, id
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToArrayAsync(cancellationToken);

        foreach (var command in commands)
        {
            command.Claim(leaseOwner, Guid.NewGuid(), databaseNow, leaseExpiresAt);
        }

        if (commands.Length > 0)
        {
            await SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return commands;
    }

    async Task<bool> IAppDbContext.CompleteShipmentCommandLeaseAsync(
        Guid commandId,
        Guid leaseToken,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        var affected = await ShipmentCommandOutbox
            .Where(command => command.Id == commandId &&
                command.Status == ShipmentCommandStatus.Leased &&
                command.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(command => command.CompletedAtUtc, completedAtUtc)
                    .SetProperty(command => command.Status, ShipmentCommandStatus.Completed)
                    .SetProperty(command => command.LastError, (string?)null)
                    .SetProperty(command => command.LastErrorCategory, (string?)null)
                    .SetProperty(command => command.LeaseOwner, (string?)null)
                    .SetProperty(command => command.LeaseToken, (Guid?)null)
                    .SetProperty(command => command.LeaseExpiresAtUtc, (DateTimeOffset?)null),
                cancellationToken);

        return affected == 1;
    }

    async Task<bool> IAppDbContext.RetryShipmentCommandLeaseAsync(
        Guid commandId,
        Guid leaseToken,
        string error,
        string errorCategory,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken)
    {
        var affected = await ShipmentCommandOutbox
            .Where(command => command.Id == commandId &&
                command.Status == ShipmentCommandStatus.Leased &&
                command.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(command => command.Status, ShipmentCommandStatus.Pending)
                    .SetProperty(command => command.LastError, error)
                    .SetProperty(command => command.LastErrorCategory, errorCategory)
                    .SetProperty(command => command.NextAttemptAtUtc, nextAttemptAtUtc)
                    .SetProperty(command => command.LeaseOwner, (string?)null)
                    .SetProperty(command => command.LeaseToken, (Guid?)null)
                    .SetProperty(command => command.LeaseExpiresAtUtc, (DateTimeOffset?)null),
                cancellationToken);

        return affected == 1;
    }

    async Task<bool> IAppDbContext.DeadLetterShipmentCommandLeaseAsync(
        Guid commandId,
        Guid leaseToken,
        string error,
        string errorCategory,
        DateTimeOffset deadLetteredAtUtc,
        CancellationToken cancellationToken)
    {
        var affected = await ShipmentCommandOutbox
            .Where(command => command.Id == commandId &&
                command.Status == ShipmentCommandStatus.Leased &&
                command.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(command => command.Status, ShipmentCommandStatus.DeadLetter)
                    .SetProperty(command => command.LastError, error)
                    .SetProperty(command => command.LastErrorCategory, errorCategory)
                    .SetProperty(command => command.DeadLetteredAtUtc, deadLetteredAtUtc)
                    .SetProperty(command => command.LeaseOwner, (string?)null)
                    .SetProperty(command => command.LeaseToken, (Guid?)null)
                    .SetProperty(command => command.LeaseExpiresAtUtc, (DateTimeOffset?)null),
                cancellationToken);

        return affected == 1;
    }

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

    public async Task<bool> TryEnqueueShipmentCommandAsync(
        Guid orderId,
        ShipmentCommandType commandType,
        string? reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("A shipment command order id is required.", nameof(orderId));
        }

        if (createdAtUtc == default)
        {
            throw new ArgumentException("A shipment command creation timestamp is required.", nameof(createdAtUtc));
        }

        var affected = await Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO shipping.shipment_command_outbox
                (id, order_id, command_type, reason, attempt_count, next_attempt_at_utc, created_at_utc, status)
            VALUES
                ({Guid.NewGuid()}, {orderId}, {commandType.ToString()}, {reason}, 0, {createdAtUtc}, {createdAtUtc}, 'Pending')
            ON CONFLICT (order_id, command_type) WHERE status IN ('Pending', 'Leased') DO NOTHING;
            """, cancellationToken);

        return affected == 1;
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

    Task<bool> ICheckoutStore.TryEnqueueShipmentCommandAsync(
        Guid orderId,
        ShipmentCommandType commandType,
        string? reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken) =>
        TryEnqueueShipmentCommandAsync(
            orderId,
            commandType,
            reason,
            createdAtUtc,
            cancellationToken);

    async Task ICheckoutStore.ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionCoreAsync(operation, cancellationToken);
    }

    async Task IAppWriteStore.ExecuteInTransactionAsync(
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

    void IAppWriteStore.Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Add(entity);
    }

    void IAppWriteStore.Update<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Update(entity);
    }

    void IAppWriteStore.Remove<TEntity>(TEntity entity)
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
