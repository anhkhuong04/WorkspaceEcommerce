using Microsoft.Extensions.Logging.Abstractions;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Payments;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Payments;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task HandleVNPayReturnAsync_Success_MarksPaidAndCreatesShipment()
    {
        var setup = CreatePendingPayment();
        var shipmentService = new FakeShipmentService();
        var service = CreateService(setup.DbContext, shipmentService: shipmentService);

        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentStatus.Paid, setup.Order.PaymentStatus);
        Assert.NotNull(setup.Order.PaidAt);
        Assert.Equal(PaymentTransactionStatus.Success, setup.Transaction.Status);
        Assert.Equal("00", setup.Transaction.GatewayResponseCode);
        Assert.NotNull(setup.Order.ShipmentId);
        Assert.NotNull(setup.Order.TrackingCode);
        Assert.True(result.Value.ShipmentCreated);
        Assert.Equal(1, shipmentService.CreateShipmentCallCount);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_DuplicateSuccess_IsIdempotentAndDoesNotCreateShipmentAgain()
    {
        var setup = CreatePendingPayment();
        var shipmentService = new FakeShipmentService();
        var service = CreateService(setup.DbContext, shipmentService: shipmentService);
        var request = CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount);

        var firstResult = await service.HandleVNPayReturnAsync(request);
        var secondResult = await service.HandleVNPayReturnAsync(request);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Success, setup.Transaction.Status);
        Assert.Equal(1, shipmentService.CreateShipmentCallCount);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_Failed_MarksPaymentFailed()
    {
        var setup = CreatePendingPayment();
        var shipmentService = new FakeShipmentService();
        var service = CreateService(setup.DbContext, shipmentService: shipmentService);

        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "99", "02", setup.Transaction.Amount));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentStatus.Failed, setup.Order.PaymentStatus);
        Assert.Null(setup.Order.PaidAt);
        Assert.Equal(PaymentTransactionStatus.Failed, setup.Transaction.Status);
        Assert.Equal("99", setup.Transaction.GatewayResponseCode);
        Assert.Null(setup.Order.ShipmentId);
        Assert.False(result.Value.ShipmentCreated);
        Assert.Equal(0, shipmentService.CreateShipmentCallCount);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_TamperedHash_ReturnsValidationAndDoesNotMutatePayment()
    {
        var setup = CreatePendingPayment();
        var shipmentService = new FakeShipmentService();
        var service = CreateService(
            setup.DbContext,
            new FakeVNPayPaymentService { IsValid = false },
            shipmentService);

        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount));

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Invalid VNPay secure hash.", result.Errors);
        Assert.Equal(PaymentStatus.Pending, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, setup.Transaction.Status);
        Assert.Null(setup.Order.ShipmentId);
        Assert.Equal(0, shipmentService.CreateShipmentCallCount);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_UnknownTxnRef_ReturnsNotFound()
    {
        var setup = CreatePendingPayment();
        var shipmentService = new FakeShipmentService();
        var service = CreateService(setup.DbContext, shipmentService: shipmentService);

        var result = await service.HandleVNPayReturnAsync(CreateCallback("MISSING-TXN", "00", "00", setup.Transaction.Amount));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Payment transaction was not found.", result.Errors);
        Assert.Equal(PaymentStatus.Pending, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, setup.Transaction.Status);
        Assert.Equal(0, shipmentService.CreateShipmentCallCount);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_WhenShipmentFails_KeepsPaymentPaid()
    {
        var setup = CreatePendingPayment();
        var shipmentService = new FakeShipmentService { ThrowOnCreate = true };
        var service = CreateService(setup.DbContext, shipmentService: shipmentService);

        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Success, setup.Transaction.Status);
        Assert.Null(setup.Order.ShipmentId);
        Assert.Equal(1, shipmentService.CreateShipmentCallCount);
    }

    private static PaymentService CreateService(
        FakeAppDbContext dbContext,
        IVNPayPaymentService? vnPayPaymentService = null,
        IShipmentService? shipmentService = null)
    {
        return new PaymentService(
            dbContext,
            vnPayPaymentService ?? new FakeVNPayPaymentService(),
            shipmentService ?? new FakeShipmentService(),
            NullLogger<PaymentService>.Instance);
    }

    private static PaymentSetup CreatePendingPayment()
    {
        var dbContext = new FakeAppDbContext();
        var order = new Order(
            Guid.NewGuid(),
            "ORD-20260710-ABCDEF",
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "123 Shipping Street, Ward 1, Ho Chi Minh",
            "Call before delivery",
            PaymentMethod.VNPay,
            "VND",
            1m);
        order.SetShippingAddressDetails(
            "123 Shipping Street",
            "Ward 1",
            "Ho Chi Minh");
        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Standing Desk",
            "DESK-001",
            100_000m,
            1,
            false);

        var transaction = new PaymentTransaction(
            Guid.NewGuid(),
            order.Id,
            PaymentProvider.VNPay,
            order.TotalAmount,
            order.CurrencyCode,
            "TXN-20260710-ABCDEF");

        dbContext.Seed(order);
        dbContext.Seed(transaction);

        return new PaymentSetup(dbContext, order, transaction);
    }

    private static VNPayCallbackRequest CreateCallback(
        string txnRef,
        string responseCode,
        string transactionStatus,
        decimal amount)
    {
        return new VNPayCallbackRequest
        {
            Parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["vnp_TxnRef"] = txnRef,
                ["vnp_ResponseCode"] = responseCode,
                ["vnp_TransactionStatus"] = transactionStatus,
                ["vnp_TransactionNo"] = "14123456",
                ["vnp_Amount"] = amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                ["vnp_SecureHash"] = "valid-hash"
            }
        };
    }

    private sealed record PaymentSetup(
        FakeAppDbContext DbContext,
        Order Order,
        PaymentTransaction Transaction);

    private sealed class FakeVNPayPaymentService : IVNPayPaymentService
    {
        public bool IsValid { get; init; } = true;

        public string CreatePaymentUrl(VNPayCreatePaymentUrlRequest request)
        {
            return "https://vnpay.test/pay";
        }

        public VNPayCallbackVerificationResult VerifyCallback(IReadOnlyDictionary<string, string?> parameters)
        {
            return new VNPayCallbackVerificationResult(
                IsValid,
                parameters.GetValueOrDefault("vnp_TxnRef"),
                TryParseAmount(parameters.GetValueOrDefault("vnp_Amount")),
                parameters.GetValueOrDefault("vnp_ResponseCode"),
                parameters.GetValueOrDefault("vnp_TransactionStatus"),
                parameters.GetValueOrDefault("vnp_TransactionNo"),
                parameters.GetValueOrDefault("vnp_SecureHash"),
                parameters.GetValueOrDefault("vnp_OrderInfo"),
                parameters);
        }

        public VNPayPaymentOutcome GetPaymentOutcome(string? responseCode, string? transactionStatus)
        {
            if (responseCode == "00" && (string.IsNullOrWhiteSpace(transactionStatus) || transactionStatus == "00"))
            {
                return VNPayPaymentOutcome.Success;
            }

            return responseCode == "24"
                ? VNPayPaymentOutcome.Cancelled
                : VNPayPaymentOutcome.Failed;
        }

        private static decimal? TryParseAmount(string? value)
        {
            return decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount)
                ? amount
                : null;
        }
    }

    private sealed class FakeShipmentService : IShipmentService
    {
        public int CreateShipmentCallCount { get; private set; }

        public bool ThrowOnCreate { get; init; }

        public Task<ShippingQuoteResponse> GetShippingQuoteAsync(
            ShippingQuoteRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ShippingQuoteResponse());
        }

        public Task<CreateShipmentResponse> CreateShipmentAsync(
            CreateShipmentRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            CreateShipmentCallCount++;
            if (ThrowOnCreate)
            {
                throw new HttpRequestException("MiniLogistics unavailable.");
            }

            return Task.FromResult(new CreateShipmentResponse
            {
                ShipmentId = Guid.NewGuid(),
                ExternalOrderId = request.ExternalOrderId,
                TrackingCode = "ML-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                Status = "PendingPickup",
                ShippingFeeAmount = 0m,
                Currency = "VND"
            });
        }

        public Task<TrackingResponse> GetTrackingAsync(
            string trackingCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TrackingResponse
            {
                TrackingCode = trackingCode,
                ExternalOrderId = "ORD-20260710-ABCDEF",
                Status = "PendingPickup",
                ShippingFeeAmount = 0m,
                Timeline = []
            });
        }
    }
}
