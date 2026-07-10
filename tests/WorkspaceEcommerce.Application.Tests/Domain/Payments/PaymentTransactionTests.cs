using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Tests.Domain.Payments;

public sealed class PaymentTransactionTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesPendingTransaction()
    {
        var orderId = Guid.NewGuid();
        var transaction = CreateTransaction(orderId: orderId, currencyCode: "vnd", txnRef: " txn-001 ");

        Assert.Equal(orderId, transaction.OrderId);
        Assert.Equal(PaymentProvider.VNPay, transaction.Provider);
        Assert.Equal(PaymentTransactionStatus.Pending, transaction.Status);
        Assert.Equal(125000m, transaction.Amount);
        Assert.Equal("VND", transaction.CurrencyCode);
        Assert.Equal("TXN-001", transaction.TxnRef);
        Assert.Null(transaction.ProcessedAt);
        Assert.False(transaction.IsTerminal);
    }

    [Fact]
    public void Constructor_EmptyOrderId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => CreateTransaction(orderId: Guid.Empty));

        Assert.Equal("Payment transaction order id cannot be empty.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveAmount_ThrowsDomainException(decimal amount)
    {
        var exception = Assert.Throws<DomainException>(() => CreateTransaction(amount: amount));

        Assert.Equal("Payment transaction amount must be greater than zero.", exception.Message);
    }

    [Fact]
    public void Constructor_MissingTxnRef_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => CreateTransaction(txnRef: " "));

        Assert.Equal("TxnRef is required.", exception.Message);
    }

    [Fact]
    public void Constructor_UnsupportedProvider_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => CreateTransaction(provider: (PaymentProvider)999));

        Assert.Equal("Payment provider is not supported.", exception.Message);
    }

    [Fact]
    public void MarkSuccess_FromPending_RecordsGatewayResult()
    {
        var processedAt = DateTimeOffset.UtcNow;
        var transaction = CreateTransaction();

        transaction.MarkSuccess(
            " gateway-001 ",
            "00",
            " Success ",
            " hash ",
            "{raw:true}",
            processedAt);

        Assert.Equal(PaymentTransactionStatus.Success, transaction.Status);
        Assert.Equal("gateway-001", transaction.GatewayTransactionNo);
        Assert.Equal("00", transaction.GatewayResponseCode);
        Assert.Equal("Success", transaction.GatewayResponseMessage);
        Assert.Equal("hash", transaction.SecureHash);
        Assert.Equal("{raw:true}", transaction.RawResponse);
        Assert.Equal(processedAt, transaction.ProcessedAt);
        Assert.True(transaction.IsTerminal);
    }

    [Fact]
    public void MarkFailed_FromPending_RecordsGatewayResult()
    {
        var processedAt = DateTimeOffset.UtcNow;
        var transaction = CreateTransaction();

        transaction.MarkFailed(null, "24", "Customer cancelled payment.", null, null, processedAt);

        Assert.Equal(PaymentTransactionStatus.Failed, transaction.Status);
        Assert.Null(transaction.GatewayTransactionNo);
        Assert.Equal("24", transaction.GatewayResponseCode);
        Assert.Equal("Customer cancelled payment.", transaction.GatewayResponseMessage);
        Assert.Equal(processedAt, transaction.ProcessedAt);
        Assert.True(transaction.IsTerminal);
    }

    [Fact]
    public void MarkCancelled_FromPending_RecordsGatewayResult()
    {
        var processedAt = DateTimeOffset.UtcNow;
        var transaction = CreateTransaction();

        transaction.MarkCancelled(null, "24", "Cancelled", null, null, processedAt);

        Assert.Equal(PaymentTransactionStatus.Cancelled, transaction.Status);
        Assert.Equal("24", transaction.GatewayResponseCode);
        Assert.Equal("Cancelled", transaction.GatewayResponseMessage);
        Assert.Equal(processedAt, transaction.ProcessedAt);
        Assert.True(transaction.IsTerminal);
    }

    [Fact]
    public void MarkTerminal_WhenAlreadyProcessed_ThrowsDomainException()
    {
        var transaction = CreateTransaction();
        transaction.MarkSuccess(null, "00", "Success", null, null, DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainException>(() =>
            transaction.MarkFailed(null, "99", "Failed", null, null, DateTimeOffset.UtcNow));

        Assert.Equal("Payment transaction has already been processed.", exception.Message);
        Assert.Equal(PaymentTransactionStatus.Success, transaction.Status);
    }

    [Fact]
    public void MarkSuccess_DefaultProcessedAt_ThrowsDomainException()
    {
        var transaction = CreateTransaction();

        var exception = Assert.Throws<DomainException>(() =>
            transaction.MarkSuccess(null, "00", "Success", null, null, default));

        Assert.Equal("Payment transaction processed timestamp is required.", exception.Message);
        Assert.Equal(PaymentTransactionStatus.Pending, transaction.Status);
    }

    private static PaymentTransaction CreateTransaction(
        Guid? orderId = null,
        PaymentProvider provider = PaymentProvider.VNPay,
        decimal amount = 125000m,
        string currencyCode = "VND",
        string txnRef = "TXN-001")
    {
        return new PaymentTransaction(
            Guid.NewGuid(),
            orderId ?? Guid.NewGuid(),
            provider,
            amount,
            currencyCode,
            txnRef);
    }
}
