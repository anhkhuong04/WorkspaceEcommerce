using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering.Receipts;

public sealed record OrderReceiptDocument(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record OrderReceiptSnapshot(
    string Language,
    string OrderCode,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string ShippingAddress,
    string? CustomerNote,
    string? CouponCode,
    string? CouponName,
    decimal Subtotal,
    decimal ShippingFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string CurrencyCode,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateTimeOffset? PaidAt,
    DateTimeOffset OrderedAt,
    DateTimeOffset GeneratedAt,
    IReadOnlyCollection<OrderReceiptLineSnapshot> Items);

public sealed record OrderReceiptLineSnapshot(
    string ProductName,
    byte[]? ProductImage,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool RequiresInstallation);
