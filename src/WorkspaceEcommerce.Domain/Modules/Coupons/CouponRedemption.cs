using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Coupons;

public sealed class CouponRedemption : Entity
{
    public CouponRedemption(
        Guid id,
        Guid couponId,
        Guid orderId,
        Guid? customerId,
        string customerPhone,
        string codeSnapshot,
        decimal discountAmount)
        : base(id)
    {
        if (couponId == Guid.Empty)
        {
            throw new DomainException("Coupon redemption coupon id cannot be empty.");
        }

        if (orderId == Guid.Empty)
        {
            throw new DomainException("Coupon redemption order id cannot be empty.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("Coupon redemption customer id cannot be empty.");
        }

        if (discountAmount <= 0m)
        {
            throw new DomainException("Coupon redemption discount amount must be greater than zero.");
        }

        CouponId = couponId;
        OrderId = orderId;
        CustomerId = customerId;
        CustomerPhone = Guard.Required(customerPhone, nameof(CustomerPhone));
        CodeSnapshot = NormalizeCode(codeSnapshot);
        DiscountAmount = discountAmount;
        RedeemedAt = DateTimeOffset.UtcNow;
    }

    public Guid CouponId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public string CustomerPhone { get; private set; }

    public string CodeSnapshot { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public DateTimeOffset RedeemedAt { get; private set; }

    private static string NormalizeCode(string code)
    {
        return Guard.Required(code, nameof(CodeSnapshot)).ToUpperInvariant();
    }
}
