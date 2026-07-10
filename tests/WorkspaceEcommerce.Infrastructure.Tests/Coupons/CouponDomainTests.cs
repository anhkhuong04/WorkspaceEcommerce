using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Infrastructure.Tests.Coupons;

public sealed class CouponDomainTests
{
    [Fact]
    public void Constructor_NormalizesCode()
    {
        var coupon = CreateCoupon(code: " summer10 ");

        Assert.Equal("SUMMER10", coupon.Code);
    }

    [Fact]
    public void ActivateAndDeactivate_UpdateStatus()
    {
        var coupon = CreateCoupon();

        coupon.Deactivate();
        Assert.False(coupon.IsActive);

        coupon.Activate();
        Assert.True(coupon.IsActive);
    }

    [Fact]
    public void UpdateDetails_RejectsUsageLimitLowerThanUsedCount()
    {
        var coupon = CreateCoupon(usageLimit: 2);
        coupon.ReserveUsage();
        coupon.ReserveUsage();

        var exception = Assert.Throws<DomainException>(() => coupon.UpdateDetails(
            "SAVE10",
            "Save 10",
            null,
            CouponDiscountType.Percentage,
            10m,
            null,
            null,
            null,
            null,
            1));

        Assert.Equal("Coupon usage limit cannot be lower than used count.", exception.Message);
    }

    [Fact]
    public void ValidateCanBeUsed_RejectsInactiveCoupon()
    {
        var coupon = CreateCoupon();
        coupon.Deactivate();

        var exception = Assert.Throws<DomainException>(() => coupon.ValidateCanBeUsed(DateTimeOffset.UtcNow));

        Assert.Equal("Coupon is inactive.", exception.Message);
    }

    [Fact]
    public void ValidateCanBeUsed_RejectsCouponOutsideEffectiveWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var notStarted = CreateCoupon(startsAt: now.AddHours(1), endsAt: now.AddHours(2));
        var expired = CreateCoupon(startsAt: now.AddHours(-2), endsAt: now.AddHours(-1));

        Assert.Equal(
            "Coupon has not started.",
            Assert.Throws<DomainException>(() => notStarted.ValidateCanBeUsed(now)).Message);
        Assert.Equal(
            "Coupon has expired.",
            Assert.Throws<DomainException>(() => expired.ValidateCanBeUsed(now)).Message);
    }

    [Fact]
    public void CalculateDiscount_PercentageAppliesMaxDiscountCap()
    {
        var coupon = CreateCoupon(
            discountType: CouponDiscountType.Percentage,
            discountValue: 20m,
            maxDiscountAmount: 50m);

        var discount = coupon.CalculateDiscount(500m);

        Assert.Equal(50m, discount);
    }

    [Fact]
    public void CalculateDiscount_FixedAmountDoesNotExceedEligibleSubtotal()
    {
        var coupon = CreateCoupon(
            discountType: CouponDiscountType.FixedAmount,
            discountValue: 200m);

        var discount = coupon.CalculateDiscount(125m);

        Assert.Equal(125m, discount);
    }

    [Fact]
    public void CalculateDiscount_RejectsMinimumSubtotalMiss()
    {
        var coupon = CreateCoupon(minimumSubtotal: 300m);

        var exception = Assert.Throws<DomainException>(() => coupon.CalculateDiscount(299m));

        Assert.Equal("Coupon minimum subtotal has not been reached.", exception.Message);
    }

    [Fact]
    public void ReserveUsage_IncrementsUsageAndRejectsLimit()
    {
        var coupon = CreateCoupon(usageLimit: 1);

        coupon.ReserveUsage();
        var exception = Assert.Throws<DomainException>(() => coupon.ReserveUsage());

        Assert.Equal(1, coupon.UsedCount);
        Assert.Equal("Coupon usage limit has been reached.", exception.Message);
    }

    [Fact]
    public void ProductTargets_RejectDuplicateProduct()
    {
        var coupon = CreateCoupon();
        var productId = Guid.NewGuid();

        coupon.AddProductTarget(Guid.NewGuid(), productId);
        var exception = Assert.Throws<DomainException>(() => coupon.AddProductTarget(Guid.NewGuid(), productId));

        Assert.Equal("Coupon product target must be unique within a coupon.", exception.Message);
    }

    [Fact]
    public void CreateLoyaltyVoucher_CreatesCustomerScopedFixedOneUseCoupon()
    {
        var customerId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var code = Coupon.FormatLoyaltyVoucherCode(voucherId);
        var startsAt = DateTimeOffset.UtcNow;
        var endsAt = startsAt.AddDays(30);

        var coupon = Coupon.CreateLoyaltyVoucher(
            voucherId,
            customerId,
            code,
            "Loyalty voucher",
            100000m,
            startsAt,
            endsAt);

        Assert.StartsWith(Coupon.LoyaltyVoucherCodePrefix, coupon.Code, StringComparison.Ordinal);
        Assert.Equal(customerId, coupon.CustomerId);
        Assert.Equal(CouponSource.Loyalty, coupon.Source);
        Assert.Equal(CouponDiscountType.FixedAmount, coupon.DiscountType);
        Assert.Equal(100000m, coupon.DiscountValue);
        Assert.Equal(1, coupon.UsageLimit);
        Assert.True(coupon.IsActive);
    }

    [Fact]
    public void ValidateCustomerEligibility_RejectsDifferentCustomer()
    {
        var coupon = Coupon.CreateLoyaltyVoucher(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Coupon.FormatLoyaltyVoucherCode(Guid.NewGuid()),
            "Loyalty voucher",
            100000m,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30));

        var exception = Assert.Throws<DomainException>(() =>
            coupon.ValidateCustomerEligibility(Guid.NewGuid()));

        Assert.Equal("Coupon is restricted to another customer.", exception.Message);
    }

    [Fact]
    public void ValidateCustomerEligibility_AllowsPublicCouponForGuest()
    {
        var coupon = CreateCoupon();

        coupon.ValidateCustomerEligibility(null);

        Assert.Null(coupon.CustomerId);
        Assert.Equal(CouponSource.Admin, coupon.Source);
    }

    [Fact]
    public void FormatLoyaltyVoucherCode_UsesLoyaltyPrefix()
    {
        var code = Coupon.FormatLoyaltyVoucherCode(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        Assert.StartsWith(Coupon.LoyaltyVoucherCodePrefix, code, StringComparison.Ordinal);
        Assert.Equal(16, code.Length);
        Assert.Equal(code.ToUpperInvariant(), code);
    }

    private static Coupon CreateCoupon(
        string code = "SAVE10",
        CouponDiscountType discountType = CouponDiscountType.Percentage,
        decimal discountValue = 10m,
        decimal? maxDiscountAmount = null,
        decimal? minimumSubtotal = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        int? usageLimit = null)
    {
        return new Coupon(
            Guid.NewGuid(),
            code,
            "Save 10",
            null,
            discountType,
            discountValue,
            maxDiscountAmount,
            minimumSubtotal,
            startsAt,
            endsAt,
            usageLimit);
    }
}
