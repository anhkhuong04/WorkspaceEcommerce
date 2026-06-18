namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class ValidateCheckoutCouponRequest
{
    public string SessionId { get; init; } = string.Empty;

    public string CouponCode { get; init; } = string.Empty;
}
