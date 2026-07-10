using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Payments;

public sealed class PaymentTransaction : Entity
{
    public PaymentTransaction(
        Guid id,
        Guid orderId,
        PaymentProvider provider,
        decimal amount,
        string currencyCode,
        string txnRef)
        : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Payment transaction order id cannot be empty.");
        }

        if (amount <= 0m)
        {
            throw new DomainException("Payment transaction amount must be greater than zero.");
        }

        OrderId = orderId;
        Provider = NormalizeProvider(provider);
        Status = PaymentTransactionStatus.Pending;
        Amount = amount;
        CurrencyCode = Guard.Required(currencyCode, nameof(CurrencyCode)).ToUpperInvariant();
        TxnRef = Guard.Required(txnRef, nameof(TxnRef)).ToUpperInvariant();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid OrderId { get; private set; }

    public PaymentProvider Provider { get; private set; }

    public PaymentTransactionStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; }

    public string TxnRef { get; private set; }

    public string? GatewayTransactionNo { get; private set; }

    public string? GatewayResponseCode { get; private set; }

    public string? GatewayResponseMessage { get; private set; }

    public string? SecureHash { get; private set; }

    public string? RawResponse { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public bool IsTerminal => Status is
        PaymentTransactionStatus.Success or
        PaymentTransactionStatus.Failed or
        PaymentTransactionStatus.Cancelled;

    public void MarkSuccess(
        string? gatewayTransactionNo,
        string? gatewayResponseCode,
        string? gatewayResponseMessage,
        string? secureHash,
        string? rawResponse,
        DateTimeOffset processedAt)
    {
        EnsurePending();
        RecordGatewayResult(
            gatewayTransactionNo,
            gatewayResponseCode,
            gatewayResponseMessage,
            secureHash,
            rawResponse,
            processedAt);
        Status = PaymentTransactionStatus.Success;
    }

    public void MarkFailed(
        string? gatewayTransactionNo,
        string? gatewayResponseCode,
        string? gatewayResponseMessage,
        string? secureHash,
        string? rawResponse,
        DateTimeOffset processedAt)
    {
        EnsurePending();
        RecordGatewayResult(
            gatewayTransactionNo,
            gatewayResponseCode,
            gatewayResponseMessage,
            secureHash,
            rawResponse,
            processedAt);
        Status = PaymentTransactionStatus.Failed;
    }

    public void MarkCancelled(
        string? gatewayTransactionNo,
        string? gatewayResponseCode,
        string? gatewayResponseMessage,
        string? secureHash,
        string? rawResponse,
        DateTimeOffset processedAt)
    {
        EnsurePending();
        RecordGatewayResult(
            gatewayTransactionNo,
            gatewayResponseCode,
            gatewayResponseMessage,
            secureHash,
            rawResponse,
            processedAt);
        Status = PaymentTransactionStatus.Cancelled;
    }

    private void RecordGatewayResult(
        string? gatewayTransactionNo,
        string? gatewayResponseCode,
        string? gatewayResponseMessage,
        string? secureHash,
        string? rawResponse,
        DateTimeOffset processedAt)
    {
        if (processedAt == default)
        {
            throw new DomainException("Payment transaction processed timestamp is required.");
        }

        GatewayTransactionNo = Guard.Optional(gatewayTransactionNo);
        GatewayResponseCode = Guard.Optional(gatewayResponseCode);
        GatewayResponseMessage = Guard.Optional(gatewayResponseMessage);
        SecureHash = Guard.Optional(secureHash);
        RawResponse = Guard.Optional(rawResponse);
        ProcessedAt = processedAt;
    }

    private void EnsurePending()
    {
        if (Status != PaymentTransactionStatus.Pending)
        {
            throw new DomainException("Payment transaction has already been processed.");
        }
    }

    private static PaymentProvider NormalizeProvider(PaymentProvider provider)
    {
        return provider switch
        {
            PaymentProvider.VNPay => provider,
            _ => throw new DomainException("Payment provider is not supported.")
        };
    }
}
