namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed record CheckoutResponse(
    OrderDto Order,
    bool PaymentRequired = false,
    string? PaymentUrl = null);
