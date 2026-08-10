using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Payments;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Payments;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task HandleVNPayReturnAsync_Success_MarksPaidAndQueuesDurableShipmentCommand()
    {
        var setup = CreatePendingPayment();
        var service = CreateService(setup.DbContext);

        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentStatus.Paid, setup.Order.PaymentStatus);
        Assert.NotNull(setup.Order.PaidAt);
        Assert.Equal(PaymentTransactionStatus.Success, setup.Transaction.Status);
        Assert.Equal("00", setup.Transaction.GatewayResponseCode);
        Assert.Null(setup.Order.ShipmentId);
        Assert.Null(setup.Order.TrackingCode);
        Assert.False(result.Value.ShipmentCreated);
        var command = Assert.Single(setup.DbContext.ShipmentCommandOutbox);
        Assert.Equal(ShipmentCommandType.Create, command.CommandType);
        Assert.Equal(ShipmentCommandStatus.Pending, command.Status);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_DuplicateSuccess_IsIdempotentAndDoesNotQueueAnotherShipmentCommand()
    {
        var setup = CreatePendingPayment();
        var service = CreateService(setup.DbContext);
        var request = CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount);

        var firstResult = await service.HandleVNPayReturnAsync(request);
        var secondResult = await service.HandleVNPayReturnAsync(request);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Success, setup.Transaction.Status);
        var command = Assert.Single(setup.DbContext.ShipmentCommandOutbox);
        Assert.Equal(ShipmentCommandType.Create, command.CommandType);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_Failed_MarksPaymentFailed()
    {
        var setup = CreatePendingPayment();
        var service = CreateService(setup.DbContext);

        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "99", "02", setup.Transaction.Amount));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentStatus.Failed, setup.Order.PaymentStatus);
        Assert.Null(setup.Order.PaidAt);
        Assert.Equal(PaymentTransactionStatus.Failed, setup.Transaction.Status);
        Assert.Equal("99", setup.Transaction.GatewayResponseCode);
        Assert.Null(setup.Order.ShipmentId);
        Assert.False(result.Value.ShipmentCreated);
        Assert.Empty(setup.DbContext.ShipmentCommandOutbox);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_TamperedHash_ReturnsValidationAndDoesNotMutatePayment()
    {
        var setup = CreatePendingPayment();
        var service = CreateService(
            setup.DbContext,
            new FakeVNPayPaymentService { IsValid = false });

        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount));

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Invalid VNPay secure hash.", result.Errors);
        Assert.Equal(PaymentStatus.Pending, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, setup.Transaction.Status);
        Assert.Null(setup.Order.ShipmentId);
        Assert.Empty(setup.DbContext.ShipmentCommandOutbox);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_UnknownTxnRef_ReturnsNotFound()
    {
        var setup = CreatePendingPayment();
        var service = CreateService(setup.DbContext);

        var result = await service.HandleVNPayReturnAsync(CreateCallback("MISSING-TXN", "00", "00", setup.Transaction.Amount));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Payment transaction was not found.", result.Errors);
        Assert.Equal(PaymentStatus.Pending, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, setup.Transaction.Status);
        Assert.Empty(setup.DbContext.ShipmentCommandOutbox);
    }

    [Fact]
    public async Task HandleVNPayReturnAsync_ExistingTerminalSuccess_RecreatesOnlyMissingActiveCommand()
    {
        var setup = CreatePendingPayment();
        var service = CreateService(setup.DbContext);

        await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount));
        var result = await service.HandleVNPayReturnAsync(CreateCallback(setup.Transaction.TxnRef, "00", "00", setup.Transaction.Amount));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, setup.Order.PaymentStatus);
        Assert.Equal(PaymentTransactionStatus.Success, setup.Transaction.Status);
        Assert.Single(setup.DbContext.ShipmentCommandOutbox);
    }

    private static PaymentService CreateService(
        FakeAppDbContext dbContext,
        IVNPayPaymentService? vnPayPaymentService = null)
    {
        return new PaymentService(
            dbContext,
            vnPayPaymentService ?? new FakeVNPayPaymentService());
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

}
