using Microsoft.Extensions.Logging.Abstractions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class AdminOrderServiceTests
{
    [Fact]
    public async Task GetOrdersAsync_ReturnsPagedOrdersWithItemCount()
    {
        var dbContext = new FakeAppDbContext();
        var firstOrder = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        firstOrder.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 100m, 1, false);
        var secondOrder = CreateOrder("ORD-20260608-0002", "0900000002", "Tran Van B");
        secondOrder.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Chair", "CHAIR-001", 50m, 2, false);
        dbContext.Seed(firstOrder, secondOrder);
        var service = CreateService(dbContext);

        var result = await service.GetOrdersAsync(new AdminOrderListRequest
        {
            Search = "0002",
            PageNumber = 1,
            PageSize = 10
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(secondOrder.Id, item.Id);
        Assert.Equal("ORD-20260608-0002", item.OrderCode);
        Assert.Equal(1, item.ItemCount);
        Assert.Equal(100m, item.TotalAmount);
    }

    [Fact]
    public async Task GetOrdersAsync_InvalidStatusFilter_ReturnsValidation()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.GetOrdersAsync(new AdminOrderListRequest
        {
            Status = (OrderStatus)999
        });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("Status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetOrderByIdAsync_ExistingOrder_ReturnsItemsAndStatusHistory()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 100m, 1, false);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.GetOrderByIdAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(order.Id, result.Value.Id);
        Assert.Single(result.Value.Items);
        var history = Assert.Single(result.Value.StatusHistory);
        Assert.Null(history.FromStatus);
        Assert.Equal(OrderStatus.Pending, history.ToStatus);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesStatusAndRecordsHistory()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest
            {
                Status = OrderStatus.Confirmed,
                Note = "Confirmed by admin"
            },
            "admin@example.com");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(OrderStatus.Confirmed, result.Value.Status);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Equal(2, result.Value.StatusHistory.Count);
        var latestHistory = result.Value.StatusHistory.Last();
        Assert.Equal(OrderStatus.Pending, latestHistory.FromStatus);
        Assert.Equal(OrderStatus.Confirmed, latestHistory.ToStatus);
        Assert.Equal("Confirmed by admin", latestHistory.Note);
        Assert.Equal("admin@example.com", latestHistory.ChangedBy);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_EarnsLoyaltyPointsAfterStatusSave()
    {
        var customerId = Guid.NewGuid();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A", customerId);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 150000m, 1, false);
        MoveToShipping(order);
        dbContext.Seed(order);
        var service = CreateService(dbContext, CreateLoyaltyService(dbContext));

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest
            {
                Status = OrderStatus.Completed,
                Note = "Delivered"
            },
            "admin@example.com");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(OrderStatus.Completed, result.Value.Status);
        Assert.Equal(2, dbContext.SaveChangesCallCount);
        var account = Assert.Single(dbContext.CustomerLoyaltyAccounts);
        Assert.Equal(customerId, account.CustomerId);
        Assert.Equal(15, account.CurrentPoints);
        Assert.Equal(15, account.TotalPointsEarned);
        var transaction = Assert.Single(dbContext.LoyaltyTransactions);
        Assert.Equal(LoyaltyTransactionType.Earn, transaction.Type);
        Assert.Equal(order.Id, transaction.OrderId);
        Assert.Equal(15, transaction.Points);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_WhenEarnAlreadyExists_DoesNotAddPointsAgain()
    {
        var customerId = Guid.NewGuid();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateDefaultTiers());
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A", customerId);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 150000m, 1, false);
        MoveToShipping(order);
        var account = new CustomerLoyaltyAccount(Guid.NewGuid(), customerId);
        account.EarnPoints(15, order.Id, "Existing earn.");
        dbContext.Seed(order);
        dbContext.Seed(account);
        var service = CreateService(dbContext, CreateLoyaltyService(dbContext));

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest { Status = OrderStatus.Completed },
            "admin@example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Equal(15, account.CurrentPoints);
        Assert.Equal(15, account.TotalPointsEarned);
        Assert.Single(dbContext.LoyaltyTransactions);
        Assert.Single(dbContext.LoyaltyTransactions.Where(transaction =>
            transaction.Type == LoyaltyTransactionType.Earn &&
            transaction.OrderId == order.Id));
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ReturnsConflict()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest { Status = OrderStatus.Completed },
            "admin@example.com");

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Order status cannot change from Pending to Completed.", result.Errors);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_MissingOrder_ReturnsNotFound()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.UpdateStatusAsync(
            Guid.NewGuid(),
            new UpdateOrderStatusRequest { Status = OrderStatus.Confirmed },
            "admin@example.com");

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Order was not found.", result.Errors);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidRequest_ReturnsValidation()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest { Status = (OrderStatus)999 },
            "admin@example.com");

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("Status", StringComparison.Ordinal));
    }

    private static AdminOrderService CreateService(FakeAppDbContext dbContext, ILoyaltyService? loyaltyService = null)
    {
        return new AdminOrderService(
            dbContext,
            new AdminOrderListRequestValidator(),
            new UpdateOrderStatusRequestValidator(),
            loyaltyService ?? new StubLoyaltyService(),
            new StubCurrentLanguageProvider(),
            NullLogger<AdminOrderService>.Instance);
    }

    private static LoyaltyService CreateLoyaltyService(FakeAppDbContext dbContext)
    {
        return new LoyaltyService(
            dbContext,
            new StubCurrentCustomerContext(),
            new LoyaltyOptions(),
            new LoyaltyTransactionListRequestValidator(),
            new RedeemLoyaltyPointsRequestValidator());
    }

    private static Order CreateOrder(string orderCode, string phone, string customerName, Guid? customerId = null)
    {
        return new Order(
            Guid.NewGuid(),
            orderCode,
            customerId,
            customerName,
            phone,
            "customer@example.com",
            "123 Shipping Street",
            "Call before delivery",
            PaymentMethod.Cod,
            "USD",
            1m);
    }

    private static void MoveToShipping(Order order)
    {
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, "admin@example.com");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, "admin@example.com");
    }

    private static LoyaltyTier[] CreateDefaultTiers()
    {
        return
        [
            new LoyaltyTier(Guid.NewGuid(), LoyaltyTierType.Bronze, 0, 0m, false),
            new LoyaltyTier(Guid.NewGuid(), LoyaltyTierType.Silver, 500, 3m, false),
            new LoyaltyTier(Guid.NewGuid(), LoyaltyTierType.Gold, 2000, 5m, true),
            new LoyaltyTier(Guid.NewGuid(), LoyaltyTierType.Platinum, 5000, 10m, true)
        ];
    }

    private sealed class StubLoyaltyService : ILoyaltyService
    {
        public Task<Result<LoyaltyAccountDto>> GetMyLoyaltyAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<PagedResult<LoyaltyTransactionDto>>> GetMyTransactionsAsync(
            LoyaltyTransactionListRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<IReadOnlyCollection<LoyaltyTierDto>>> GetTiersAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<RedeemLoyaltyPointsResponse>> RedeemPointsAsync(
            RedeemLoyaltyPointsRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> EarnForCompletedOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class StubCurrentCustomerContext : ICurrentCustomerContext
    {
        public Guid? CustomerId => null;

        public string? Email => null;
    }

    private sealed class StubCurrentLanguageProvider : WorkspaceEcommerce.Application.Common.Localization.ICurrentLanguageProvider
    {
        public string CurrentLanguage => "en";
    }
}
