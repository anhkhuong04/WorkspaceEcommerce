using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Payments;

public enum OrderPaymentReceiptKind { ManualTransfer = 0, CodCollected = 1, CodRemitted = 2 }

/// <summary>Exact-amount operational confirmation; collection and remittance are distinct facts.</summary>
public sealed class OrderPaymentReceipt : Entity
{
    private OrderPaymentReceipt() { }
    public OrderPaymentReceipt(Guid id, Guid orderId, OrderPaymentReceiptKind kind, decimal amount, string currency,
        string reference, string reason, string actor, DateTimeOffset now) : base(id)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || !Enum.IsDefined(kind) || now == default)
            throw new DomainException("Payment receipt identifiers, kind and timestamp are required.");
        if (amount < 0 || amount != decimal.Truncate(amount) || amount > 999_999_999_999_999_999m || currency != "VND")
            throw new DomainException("Payment confirmation requires a whole nonnegative VND amount.");
        OrderId = orderId; Kind = kind; Amount = amount; Currency = currency;
        Reference = Required(reference, 120); Reason = Required(reason, 500); Actor = Required(actor, 256); OccurredAtUtc = now;
    }
    public Guid OrderId { get; private set; }
    public OrderPaymentReceiptKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    private static string Required(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max
        ? value.Trim() : throw new DomainException($"Payment receipt text is required and must be at most {max} characters.");
}
