using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Shipments;

public sealed class OrderShipmentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RetryCreateAsync_ValidOrder_QueuesThenCreatesShipmentWithStableIdempotencyKey()
    {
        var dbContext = new FakeAppDbContext();
        var (order, variant) = CreateOrderWithVariant();
        dbContext.Seed(order);
        dbContext.Seed(variant);
        var provider = new StubShipmentService();
        var service = CreateService(dbContext, provider);

        var result = await service.RetryCreateAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, provider.CreateCallCount);
        var command = Assert.Single(dbContext.ShipmentCommandOutbox);
        Assert.Equal(ShipmentCommandStatus.Pending, command.Status);

        var processed = await service.ProcessDueCommandsAsync(batchSize: 10);

        Assert.Equal(1, processed);
        Assert.Equal(order.OrderCode, provider.LastIdempotencyKey);
        Assert.Equal(1, provider.CreateCallCount);
        Assert.Equal(provider.CreateResponse.TrackingCode, order.TrackingCode);
        Assert.Single(dbContext.OrderShipments);
        Assert.Single(dbContext.ShipmentTimelineEntries);
    }

    [Fact]
    public async Task RetryCreateAsync_ExistingShipment_ReturnsConflictWithoutProviderCall()
    {
        var dbContext = new FakeAppDbContext();
        var (order, variant) = CreateOrderWithVariant();
        order.UpdateShipmentInfo("ML-EXISTING", Guid.NewGuid());
        dbContext.Seed(order);
        dbContext.Seed(variant);
        dbContext.Seed(CreateShipment(order));
        var provider = new StubShipmentService();
        var service = CreateService(dbContext, provider);

        var result = await service.RetryCreateAsync(order.Id);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, provider.CreateCallCount);
    }

    [Fact]
    public async Task RetryCreateAsync_MissingOrder_ReturnsNotFound()
    {
        var service = CreateService(new FakeAppDbContext(), new StubShipmentService());

        var result = await service.RetryCreateAsync(Guid.NewGuid());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task RetryCreateAsync_TransientProviderFailure_RetriesTheLeasedCommandWithoutDuplication()
    {
        var dbContext = new FakeAppDbContext();
        var (order, variant) = CreateOrderWithVariant();
        dbContext.Seed(order);
        dbContext.Seed(variant);
        var provider = new StubShipmentService
        {
            CreateException = new HttpRequestException("network unavailable")
        };
        var service = CreateService(dbContext, provider);

        var first = await service.RetryCreateAsync(order.Id);
        var second = await service.RetryCreateAsync(order.Id);
        var processed = await service.ProcessDueCommandsAsync(batchSize: 10);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, processed);
        var command = Assert.Single(dbContext.ShipmentCommandOutbox);
        Assert.Equal(ShipmentCommandStatus.Pending, command.Status);
        Assert.Equal(1, command.AttemptCount);
        Assert.Equal(1, provider.CreateCallCount);
    }

    [Fact]
    public async Task CancelAsync_CancellableShipment_QueuesThenUpdatesProviderAndOrderStatus()
    {
        var dbContext = new FakeAppDbContext();
        var (order, _) = CreateOrderWithVariant();
        order.UpdateShipmentInfo("ML-CANCEL", Guid.NewGuid());
        var shipment = CreateShipment(order);
        dbContext.Seed(order);
        dbContext.Seed(shipment);
        var provider = new StubShipmentService
        {
            CancelResponse = new TrackingResponse
            {
                TrackingCode = shipment.TrackingCode,
                ExternalOrderId = order.OrderCode,
                Status = "Cancelled",
                ShippingFeeAmount = shipment.ShippingFeeAmount,
                Currency = "VND",
                Timeline =
                [
                    new TrackingTimelineEntry
                    {
                        Status = "Cancelled",
                        Note = "Cancelled by shop.",
                        ChangedAtUtc = Now
                    }
                ]
            }
        };
        var service = CreateService(dbContext, provider);

        var result = await service.CancelAsync(order.Id, "Customer request");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, provider.CancelCallCount);
        var command = Assert.Single(dbContext.ShipmentCommandOutbox);
        Assert.Equal(ShipmentCommandType.Cancel, command.CommandType);

        var processed = await service.ProcessDueCommandsAsync(batchSize: 10);

        Assert.Equal(1, processed);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Cancelled", shipment.ProviderStatus);
        Assert.Equal(1, provider.CancelCallCount);
        Assert.Equal($"{order.OrderCode}:cancel", provider.LastCancelIdempotencyKey);
        Assert.Single(dbContext.ShipmentTimelineEntries);
    }

    [Fact]
    public async Task CancelAsync_ProviderConflict_DeadLettersTheQueuedCommand()
    {
        var dbContext = new FakeAppDbContext();
        var (order, _) = CreateOrderWithVariant();
        order.UpdateShipmentInfo("ML-CONFLICT", Guid.NewGuid());
        dbContext.Seed(order);
        dbContext.Seed(CreateShipment(order));
        var provider = new StubShipmentService
        {
            CancelException = new HttpRequestException("already assigned", null, HttpStatusCode.Conflict)
        };
        var service = CreateService(dbContext, provider);

        var result = await service.CancelAsync(order.Id, "Customer request");
        var processed = await service.ProcessDueCommandsAsync(batchSize: 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, processed);
        Assert.Equal(1, provider.CancelCallCount);
        var command = Assert.Single(dbContext.ShipmentCommandOutbox);
        Assert.Equal(ShipmentCommandStatus.DeadLetter, command.Status);
        Assert.Equal("Conflict", command.LastErrorCategory);
    }

    [Fact]
    public async Task CancelAsync_OrderWithoutShipment_ReturnsConflict()
    {
        var dbContext = new FakeAppDbContext();
        var (order, _) = CreateOrderWithVariant();
        dbContext.Seed(order);
        var provider = new StubShipmentService();
        var service = CreateService(dbContext, provider);

        var result = await service.CancelAsync(order.Id, "Customer request");

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, provider.CancelCallCount);
    }

    [Fact]
    public async Task CancelAsync_TerminalOrder_DoesNotCallProvider()
    {
        var dbContext = new FakeAppDbContext();
        var (order, _) = CreateOrderWithVariant();
        order.UpdateShipmentInfo("ML-COMPLETE", Guid.NewGuid());
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "test");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, "test");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, "test");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, null, "test");
        dbContext.Seed(order);
        dbContext.Seed(CreateShipment(order));
        var provider = new StubShipmentService();
        var service = CreateService(dbContext, provider);

        var result = await service.CancelAsync(order.Id, "Too late");

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, provider.CancelCallCount);
    }

    private static OrderShipmentService CreateService(
        FakeAppDbContext dbContext,
        IShipmentService provider)
    {
        return new OrderShipmentService(
            dbContext,
            provider,
            new StubCurrentCustomerContext(),
            new StubLoyaltyService(),
            new StubTimeProvider(Now),
            NullLogger<OrderShipmentService>.Instance);
    }

    private static (Order Order, ProductVariant Variant) CreateOrderWithVariant()
    {
        var variant = new ProductVariant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SHIP-TEST",
            "Shipment Test Variant",
            null,
            null,
            100000m,
            null,
            10,
            false,
            weightKg: 1m,
            lengthCm: 20m,
            widthCm: 15m,
            heightCm: 10m);
        var order = new Order(
            Guid.NewGuid(),
            $"ORD-SHIP-{Guid.NewGuid():N}"[..24],
            null,
            "Shipment Customer",
            "0900000000",
            null,
            "9 Le Loi, Ben Nghe, Ho Chi Minh City",
            null,
            PaymentMethod.Cod,
            "VND",
            1m);
        order.SetShippingAddressDetails("9 Le Loi", "Ben Nghe", "Ho Chi Minh City");
        order.AddItem(Guid.NewGuid(), variant.Id, "Shipment Test Product", variant.Sku, variant.Price, 1, false);
        return (order, variant);
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

    private sealed class StubShipmentService : IShipmentService
    {
        public CreateShipmentResponse CreateResponse { get; } = new()
        {
            ShipmentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            ExternalOrderId = string.Empty,
            TrackingCode = "ML-CREATED",
            Status = "PendingPickup",
            ShippingFeeAmount = 30000m,
            Currency = "VND"
        };

        public HttpRequestException? QuoteException { get; init; }

        public HttpRequestException? CreateException { get; init; }

        public HttpRequestException? CancelException { get; init; }

        public TrackingResponse? CancelResponse { get; init; }

        public int CreateCallCount { get; private set; }

        public int CancelCallCount { get; private set; }

        public string? LastIdempotencyKey { get; private set; }

        public string? LastCancelIdempotencyKey { get; private set; }

        public Task<ShippingQuoteResponse> GetShippingQuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default)
        {
            if (QuoteException is not null)
            {
                throw QuoteException;
            }

            return Task.FromResult(new ShippingQuoteResponse { TotalFeeAmount = 30000m, Currency = "VND" });
        }

        public Task<CreateShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            LastIdempotencyKey = idempotencyKey;
            if (CreateException is not null)
            {
                throw CreateException;
            }

            return Task.FromResult(new CreateShipmentResponse
            {
                ShipmentId = CreateResponse.ShipmentId,
                ExternalOrderId = request.ExternalOrderId,
                TrackingCode = CreateResponse.TrackingCode,
                Status = CreateResponse.Status,
                ShippingFeeAmount = CreateResponse.ShippingFeeAmount,
                Currency = CreateResponse.Currency
            });
        }

        public Task<TrackingResponse> GetTrackingAsync(string trackingCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TrackingResponse> CancelShipmentAsync(string trackingCode, string reason, CancellationToken cancellationToken = default)
        {
            CancelCallCount++;
            if (CancelException is not null)
            {
                throw CancelException;
            }

            return Task.FromResult(CancelResponse ?? throw new InvalidOperationException("Cancel response is not configured."));
        }

        public Task<TrackingResponse> CancelShipmentAsync(
            string trackingCode,
            string reason,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            LastCancelIdempotencyKey = idempotencyKey;
            return CancelShipmentAsync(trackingCode, reason, cancellationToken);
        }
    }

    private sealed class StubCurrentCustomerContext : ICurrentCustomerContext
    {
        public Guid? CustomerId => null;

        public string? Email => null;
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubLoyaltyService : ILoyaltyService
    {
        public Task<Result> EarnForCompletedOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<LoyaltyAccountDto>> GetMyLoyaltyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<PagedResult<LoyaltyTransactionDto>>> GetMyTransactionsAsync(LoyaltyTransactionListRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<IReadOnlyCollection<LoyaltyTierDto>>> GetTiersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<RedeemLoyaltyPointsResponse>> RedeemPointsAsync(RedeemLoyaltyPointsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
