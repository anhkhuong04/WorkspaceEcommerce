using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Loyalty;

public sealed class LoyaltyServiceTests
{
    [Fact]
    public async Task GetMyLoyaltyAsync_WhenCustomerIsNotAuthenticated_ReturnsUnauthorized()
    {
        var service = CreateService(new FakeAppDbContext(), customerId: null);

        var result = await service.GetMyLoyaltyAsync();

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task GetMyLoyaltyAsync_WhenAccountDoesNotExist_ReturnsEmptyBronzeAccount()
    {
        var customerId = Guid.NewGuid();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        var service = CreateService(dbContext, customerId);

        var result = await service.GetMyLoyaltyAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.AccountId);
        Assert.Equal(customerId, result.Value.CustomerId);
        Assert.Equal(0, result.Value.CurrentPoints);
        Assert.Equal(LoyaltyTierType.Bronze, result.Value.CurrentTier);
        Assert.Equal(LoyaltyTierType.Silver, result.Value.NextTier);
        Assert.Equal(500, result.Value.PointsToNextTier);
    }

    [Fact]
    public async Task EarnForCompletedOrderAsync_ForCustomerOrder_CreatesAccountAndEarnTransaction()
    {
        var customerId = Guid.NewGuid();
        var order = CreateCompletedOrder(customerId, subtotal: 150000m, exchangeRate: 1m);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        dbContext.Seed(order);
        var service = CreateService(dbContext, customerId: null);

        var result = await service.EarnForCompletedOrderAsync(order.Id);

        Assert.True(result.IsSuccess);
        var account = Assert.Single(dbContext.CustomerLoyaltyAccounts);
        Assert.Equal(customerId, account.CustomerId);
        Assert.Equal(15, account.CurrentPoints);
        Assert.Equal(15, account.TotalPointsEarned);
        Assert.Equal(LoyaltyTierType.Bronze, account.CurrentTier);
        var transaction = Assert.Single(dbContext.LoyaltyTransactions);
        Assert.Equal(LoyaltyTransactionType.Earn, transaction.Type);
        Assert.Equal(order.Id, transaction.OrderId);
        Assert.Equal(15, transaction.Points);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task EarnForCompletedOrderAsync_WhenEarnReachesTierThreshold_UpgradesTier()
    {
        var customerId = Guid.NewGuid();
        var order = CreateCompletedOrder(customerId, subtotal: 6000000m, exchangeRate: 1m);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        dbContext.Seed(order);
        var service = CreateService(dbContext, customerId: null);

        var result = await service.EarnForCompletedOrderAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(LoyaltyTierType.Silver, dbContext.CustomerLoyaltyAccounts.Single().CurrentTier);
    }

    [Fact]
    public async Task EarnForCompletedOrderAsync_ForGuestOrder_ReturnsSuccessNoOp()
    {
        var order = CreateCompletedOrder(customerId: null, subtotal: 150000m, exchangeRate: 1m);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        dbContext.Seed(order);
        var service = CreateService(dbContext, customerId: null);

        var result = await service.EarnForCompletedOrderAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(dbContext.CustomerLoyaltyAccounts);
        Assert.Empty(dbContext.LoyaltyTransactions);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task EarnForCompletedOrderAsync_WhenOrderAlreadyEarned_ReturnsSuccessNoOp()
    {
        var customerId = Guid.NewGuid();
        var order = CreateCompletedOrder(customerId, subtotal: 150000m, exchangeRate: 1m);
        var account = new CustomerLoyaltyAccount(Guid.NewGuid(), customerId);
        account.EarnPoints(15, order.Id, "Existing earn.");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        dbContext.Seed(order);
        dbContext.Seed(account);
        var service = CreateService(dbContext, customerId: null);

        var result = await service.EarnForCompletedOrderAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(dbContext.LoyaltyTransactions);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task EarnForCompletedOrderAsync_WhenOrderIsNotCompleted_ReturnsConflict()
    {
        var order = CreatePendingOrder(Guid.NewGuid(), subtotal: 150000m, exchangeRate: 1m);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(order);
        var service = CreateService(dbContext, customerId: null);

        var result = await service.EarnForCompletedOrderAsync(order.Id);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Order must be completed before earning loyalty points.", result.Errors);
    }

    [Fact]
    public async Task RedeemPointsAsync_ValidRequest_CreatesCustomerScopedLoyaltyVoucher()
    {
        var customerId = Guid.NewGuid();
        var account = new CustomerLoyaltyAccount(Guid.NewGuid(), customerId);
        account.EarnPoints(150, Guid.NewGuid(), "Earned from order.");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(account);
        var service = CreateService(dbContext, customerId);

        var result = await service.RedeemPointsAsync(new RedeemLoyaltyPointsRequest { Points = 100 });

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value!.RemainingPoints);
        Assert.Equal(100000m, result.Value.DiscountAmount);
        Assert.StartsWith(Coupon.LoyaltyVoucherCodePrefix, result.Value.VoucherCode, StringComparison.Ordinal);
        var coupon = Assert.Single(dbContext.Coupons);
        Assert.Equal(customerId, coupon.CustomerId);
        Assert.Equal(CouponSource.Loyalty, coupon.Source);
        Assert.Equal(1, coupon.UsageLimit);
        var redeemTransaction = dbContext.LoyaltyTransactions.Single(transaction => transaction.Type == LoyaltyTransactionType.Redeem);
        Assert.Equal(coupon.Id, redeemTransaction.VoucherId);
        Assert.Equal(-100, redeemTransaction.Points);
        Assert.Equal(1, dbContext.TransactionCallCount);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task RedeemPointsAsync_WhenCustomerIsNotAuthenticated_ReturnsUnauthorized()
    {
        var service = CreateService(new FakeAppDbContext(), customerId: null);

        var result = await service.RedeemPointsAsync(new RedeemLoyaltyPointsRequest { Points = 100 });

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task RedeemPointsAsync_WhenBalanceIsInsufficient_ReturnsValidation()
    {
        var customerId = Guid.NewGuid();
        var account = new CustomerLoyaltyAccount(Guid.NewGuid(), customerId);
        account.EarnPoints(50, Guid.NewGuid(), "Earned from order.");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(account);
        var service = CreateService(dbContext, customerId);

        var result = await service.RedeemPointsAsync(new RedeemLoyaltyPointsRequest { Points = 100 });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Loyalty account does not have enough points.", result.Errors);
        Assert.Empty(dbContext.Coupons);
        Assert.Single(dbContext.LoyaltyTransactions);
    }

    [Fact]
    public async Task GetMyTransactionsAsync_ReturnsPagedCurrentCustomerTransactions()
    {
        var customerId = Guid.NewGuid();
        var account = new CustomerLoyaltyAccount(Guid.NewGuid(), customerId);
        account.EarnPoints(20, Guid.NewGuid(), "Earned 20.");
        account.RedeemPoints(5, Guid.NewGuid(), "Redeemed 5.");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(account);
        var service = CreateService(dbContext, customerId);

        var result = await service.GetMyTransactionsAsync(new LoyaltyTransactionListRequest
        {
            PageNumber = 1,
            PageSize = 1
        });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.True(result.Value.HasNextPage);
    }

    [Fact]
    public async Task GetTiersAsync_ReturnsConfiguredTiers()
    {
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        var service = CreateService(dbContext, Guid.NewGuid());

        var result = await service.GetTiersAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Count);
        Assert.Contains(result.Value, tier => tier.Type == LoyaltyTierType.Platinum && tier.FreeShippingEnabled);
    }

    private static LoyaltyService CreateService(FakeAppDbContext dbContext, Guid? customerId)
    {
        return new LoyaltyService(
            dbContext,
            new StubCurrentCustomerContext(customerId),
            new LoyaltyOptions(),
            new LoyaltyTransactionListRequestValidator(),
            new RedeemLoyaltyPointsRequestValidator());
    }

    private static Order CreatePendingOrder(Guid? customerId, decimal subtotal, decimal exchangeRate)
    {
        var order = new Order(
            Guid.NewGuid(),
            $"ORD-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            customerId,
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "123 Shipping Street",
            null,
            PaymentMethod.Cod,
            "VND",
            exchangeRate);

        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Standing Desk",
            "DESK-001",
            subtotal,
            1,
            requiresInstallation: false);

        return order;
    }

    private static Order CreateCompletedOrder(Guid? customerId, decimal subtotal, decimal exchangeRate)
    {
        var order = CreatePendingOrder(customerId, subtotal, exchangeRate);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, null, "admin@example.com");

        return order;
    }

    private static LoyaltyTier[] CreateDefaultTiers()
    {
        return
        [
            CreateTier(LoyaltyTierType.Bronze, 0, 0m, false),
            CreateTier(LoyaltyTierType.Silver, 500, 3m, false),
            CreateTier(LoyaltyTierType.Gold, 2000, 5m, true),
            CreateTier(LoyaltyTierType.Platinum, 5000, 10m, true)
        ];
    }

    private static LoyaltyTier CreateTier(
        LoyaltyTierType type,
        int minTotalPointsEarned,
        decimal discountPercent,
        bool freeShippingEnabled)
    {
        return new LoyaltyTier(
            Guid.NewGuid(),
            type,
            minTotalPointsEarned,
            discountPercent,
            freeShippingEnabled);
    }

    private sealed class StubCurrentCustomerContext(Guid? customerId) : ICurrentCustomerContext
    {
        public Guid? CustomerId => customerId;

        public string? Email => customerId.HasValue ? "customer@example.com" : null;
    }
}
