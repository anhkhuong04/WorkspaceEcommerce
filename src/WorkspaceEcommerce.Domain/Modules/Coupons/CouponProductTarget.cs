using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Coupons;

public sealed class CouponProductTarget : Entity
{
    public CouponProductTarget(Guid id, Guid couponId, Guid productId)
        : base(id)
    {
        if (couponId == Guid.Empty)
        {
            throw new DomainException("Coupon target coupon id cannot be empty.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("Coupon target product id cannot be empty.");
        }

        CouponId = couponId;
        ProductId = productId;
    }

    public Guid CouponId { get; private set; }

    public Guid ProductId { get; private set; }
}
