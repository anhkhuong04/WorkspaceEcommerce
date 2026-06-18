namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed record CheckoutCouponValidationResponse(
    string CouponCode,
    decimal DiscountAmount,
    decimal EligibleSubtotal,
    decimal Subtotal,
    decimal TotalAmount,
    string Message);
