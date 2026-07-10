using System.Text.Json.Serialization;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Modules.Payments;

public sealed class VNPayCallbackRequest
{
    public IReadOnlyDictionary<string, string?> Parameters { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
}

public sealed record PaymentResultDto(
    Guid OrderId,
    string OrderCode,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateTimeOffset? PaidAt,
    bool ShipmentCreated,
    Guid? ShipmentId,
    string? TrackingCode,
    PaymentTransactionDto? Transaction,
    string? GatewayResponseCode,
    string? Message);

public sealed record PaymentTransactionDto(
    Guid Id,
    PaymentProvider Provider,
    PaymentTransactionStatus Status,
    decimal Amount,
    string CurrencyCode,
    string TxnRef,
    string? GatewayTransactionNo,
    string? GatewayResponseCode,
    string? GatewayResponseMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public sealed record VNPayIpnResponseDto(
    [property: JsonPropertyName("RspCode")] string RspCode,
    [property: JsonPropertyName("Message")] string Message);
