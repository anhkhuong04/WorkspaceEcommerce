using Microsoft.Extensions.Logging.Abstractions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Shipments;

public sealed class ShipmentWebhookServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("PendingPickup", OrderStatus.Confirmed)]
    [InlineData("Assigned", OrderStatus.Processing)]
    [InlineData("PickingUp", OrderStatus.Processing)]
    [InlineData("PickedUp", OrderStatus.Processing)]
    [InlineData("InTransit", OrderStatus.Shipping)]
    [InlineData("Delivering", OrderStatus.Shipping)]
    [InlineData("Delivered", OrderStatus.Completed)]
    [InlineData("DeliveryFailed", OrderStatus.FailedDelivery)]
    [InlineData("Returned", OrderStatus.FailedDelivery)]
    [InlineData("Cancelled", OrderStatus.Cancelled)]
    public void MapOrderStatus_ProviderContract_MapsExpectedStatus(string providerStatus, OrderStatus expected)
    {
        Assert.Equal(expected, ShipmentProviderContract.MapOrderStatus(providerStatus));
    }

    [Fact]
    public async Task HandleAsync_Delivered_CompletesOrderAndAwardsLoyaltyOnce()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder();
        var shipment = CreateShipment(order);
        dbContext.Seed(order);
        dbContext.Seed(shipment);
        var loyalty = new StubLoyaltyService();
        var service = CreateService(dbContext, loyalty);
        var payload = CreatePayload(order, shipment, "Delivered");

        var first = await service.HandleAsync(payload);
        var duplicate = await service.HandleAsync(payload);

        Assert.True(first.IsSuccess);
        Assert.False(first.Value!.IsDuplicate);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal("Delivered", shipment.ProviderStatus);
        Assert.Equal(1, loyalty.EarnCallCount);
        Assert.Equal(4, dbContext.OrderStatusHistories.Count());
        Assert.Single(dbContext.ShipmentTimelineEntries);
        Assert.Single(dbContext.ShipmentEventInbox);
        Assert.True(duplicate.IsSuccess);
        Assert.True(duplicate.Value!.IsDuplicate);
        Assert.Equal(1, loyalty.EarnCallCount);
    }

    [Fact]
    public async Task HandleAsync_TrackingCodeMismatch_ReturnsConflictWithoutMutation()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder();
        var shipment = CreateShipment(order);
        dbContext.Seed(order);
        dbContext.Seed(shipment);
        var service = CreateService(dbContext, new StubLoyaltyService());
        var payload = CreatePayload(order, shipment, "InTransit") with { TrackingCode = "ML-WRONG" };

        var result = await service.HandleAsync(payload);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal("PendingPickup", shipment.ProviderStatus);
        Assert.Empty(dbContext.ShipmentEventInbox);
    }

    [Fact]
    public async Task HandleAsync_OlderEvent_DoesNotRegressShipmentOrOrder()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder();
        var shipment = CreateShipment(order);
        shipment.ApplyProviderState("InTransit", 30000m, "VND", Now, Now);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "test");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, "test");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, "test");
        dbContext.Seed(order);
        dbContext.Seed(shipment);
        var service = CreateService(dbContext, new StubLoyaltyService());
        var payload = CreatePayload(order, shipment, "PendingPickup") with { ChangedAtUtc = Now.AddMinutes(-5) };

        var result = await service.HandleAsync(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal("InTransit", shipment.ProviderStatus);
        Assert.Equal(OrderStatus.Shipping, order.Status);
    }

    [Fact]
    public async Task HandleAsync_UnknownProviderStatus_ReturnsValidationWithoutMutation()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder();
        var shipment = CreateShipment(order);
        dbContext.Seed(order);
        dbContext.Seed(shipment);
        var service = CreateService(dbContext, new StubLoyaltyService());

        var result = await service.HandleAsync(CreatePayload(order, shipment, "UnknownStatus"));

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("PendingPickup", shipment.ProviderStatus);
        Assert.Empty(dbContext.ShipmentEventInbox);
        Assert.Empty(dbContext.ShipmentTimelineEntries);
    }

    private static ShipmentWebhookService CreateService(FakeAppDbContext dbContext, ILoyaltyService loyaltyService)
    {
        return new ShipmentWebhookService(
            dbContext,
            loyaltyService,
            new StubTimeProvider(Now),
            NullLogger<ShipmentWebhookService>.Instance);
    }

    private static Order CreateOrder()
    {
        var order = new Order(
            Guid.NewGuid(),
            "ORD-20260802-WEBHOOK",
            customerId: null,
            "Webhook Customer",
            "0900000000",
            null,
            "1 Integration Street",
            null,
            PaymentMethod.Cod,
            "VND",
            1m);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Item", "ITEM-1", 100000m, 1, false);
        order.UpdateShipmentInfo("ML-WEBHOOK-1", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        return order;
    }

    private static OrderShipment CreateShipment(Order order)
    {
        return new OrderShipment(
            Guid.NewGuid(),
            order.Id,
            ShipmentProviderContract.ProviderName,
            order.ShipmentId!.Value,
            order.TrackingCode!,
            "PendingPickup",
            30000m,
            "VND",
            Now.AddMinutes(-10));
    }

    private static ShipmentWebhookPayload CreatePayload(Order order, OrderShipment shipment, string status)
    {
        return new ShipmentWebhookPayload(
            Guid.NewGuid(),
            ShipmentProviderContract.ShipmentStatusChangedEvent,
            shipment.TrackingCode,
            order.OrderCode,
            status,
            Now);
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubLoyaltyService : ILoyaltyService
    {
        public int EarnCallCount { get; private set; }

        public Task<Result> EarnForCompletedOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            EarnCallCount++;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<LoyaltyAccountDto>> GetMyLoyaltyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<PagedResult<LoyaltyTransactionDto>>> GetMyTransactionsAsync(LoyaltyTransactionListRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<IReadOnlyCollection<LoyaltyTierDto>>> GetTiersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<RedeemLoyaltyPointsResponse>> RedeemPointsAsync(RedeemLoyaltyPointsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
