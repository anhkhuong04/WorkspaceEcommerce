using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class CheckoutRequest
{
    public string SessionId { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerPhone { get; init; } = string.Empty;

    public string? CustomerEmail { get; init; }

    public string ShippingAddress { get; init; } = string.Empty;

    public string ShippingStreet { get; init; } = string.Empty;

    public string ShippingWard { get; init; } = string.Empty;

    public string ShippingProvince { get; init; } = string.Empty;

    public string? Note { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public string? CouponCode { get; init; }

    public string? ClientIpAddress { get; init; }
}
