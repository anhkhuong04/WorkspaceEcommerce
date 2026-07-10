namespace WorkspaceEcommerce.Application.Abstractions.Payments;

public sealed class VNPayCreatePaymentUrlRequest
{
    public string TxnRef { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string OrderInfo { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? ReturnUrl { get; init; }

    public string? Locale { get; init; }
}

public sealed record VNPayCallbackVerificationResult(
    bool IsValid,
    string? TxnRef,
    decimal? Amount,
    string? ResponseCode,
    string? TransactionStatus,
    string? GatewayTransactionNo,
    string? SecureHash,
    string? OrderInfo,
    IReadOnlyDictionary<string, string?> Parameters);

public enum VNPayPaymentOutcome
{
    Success = 0,
    Failed = 1,
    Cancelled = 2
}
