using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Coupons;

public sealed class Coupon : Entity
{
    private readonly List<CouponProductTarget> _productTargets = [];

    public Coupon(
        Guid id,
        string code,
        string name,
        string? description,
        CouponDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal? minimumSubtotal,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        int? usageLimit,
        bool isActive = true)
        : base(id)
    {
        Code = NormalizeCode(code);
        Name = Guard.Required(name, nameof(Name));
        Description = Guard.Optional(description);
        DiscountType = discountType;
        DiscountValue = ValidateDiscountValue(discountType, discountValue);
        MaxDiscountAmount = ValidateOptionalMoney(maxDiscountAmount, nameof(MaxDiscountAmount));
        MinimumSubtotal = ValidateOptionalMoney(minimumSubtotal, nameof(MinimumSubtotal));
        StartsAt = startsAt;
        EndsAt = endsAt;
        ValidateEffectiveWindow(StartsAt, EndsAt);
        UsageLimit = ValidateUsageLimit(usageLimit);
        UsedCount = 0;
        IsActive = isActive;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public CouponDiscountType DiscountType { get; private set; }

    public decimal DiscountValue { get; private set; }

    public decimal? MaxDiscountAmount { get; private set; }

    public decimal? MinimumSubtotal { get; private set; }

    public DateTimeOffset? StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public int? UsageLimit { get; private set; }

    public int UsedCount { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<CouponProductTarget> ProductTargets => _productTargets;

    public bool TargetsAllProducts => _productTargets.Count == 0;

    public void UpdateDetails(
        string code,
        string name,
        string? description,
        CouponDiscountType discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal? minimumSubtotal,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        int? usageLimit)
    {
        var normalizedCode = NormalizeCode(code);
        var normalizedName = Guard.Required(name, nameof(Name));
        var normalizedDescription = Guard.Optional(description);
        var normalizedDiscountValue = ValidateDiscountValue(discountType, discountValue);
        var normalizedMaxDiscountAmount = ValidateOptionalMoney(maxDiscountAmount, nameof(MaxDiscountAmount));
        var normalizedMinimumSubtotal = ValidateOptionalMoney(minimumSubtotal, nameof(MinimumSubtotal));
        var normalizedUsageLimit = ValidateUsageLimit(usageLimit);
        ValidateEffectiveWindow(startsAt, endsAt);

        if (normalizedUsageLimit is not null && UsedCount > normalizedUsageLimit.Value)
        {
            throw new DomainException("Coupon usage limit cannot be lower than used count.");
        }

        Code = normalizedCode;
        Name = normalizedName;
        Description = normalizedDescription;
        DiscountType = discountType;
        DiscountValue = normalizedDiscountValue;
        MaxDiscountAmount = normalizedMaxDiscountAmount;
        MinimumSubtotal = normalizedMinimumSubtotal;
        StartsAt = startsAt;
        EndsAt = endsAt;
        UsageLimit = normalizedUsageLimit;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public CouponProductTarget AddProductTarget(Guid id, Guid productId)
    {
        if (_productTargets.Any(target => target.ProductId == productId))
        {
            throw new DomainException("Coupon product target must be unique within a coupon.");
        }

        var target = new CouponProductTarget(id, Id, productId);
        _productTargets.Add(target);
        Touch();

        return target;
    }

    public void RemoveProductTarget(Guid productId)
    {
        var target = _productTargets.FirstOrDefault(existing => existing.ProductId == productId);
        if (target is null)
        {
            return;
        }

        _productTargets.Remove(target);
        Touch();
    }

    public bool IsProductEligible(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Coupon product id cannot be empty.");
        }

        return TargetsAllProducts || _productTargets.Any(target => target.ProductId == productId);
    }

    public void ValidateCanBeUsed(DateTimeOffset at)
    {
        if (!IsActive)
        {
            throw new DomainException("Coupon is inactive.");
        }

        if (StartsAt is not null && at < StartsAt.Value)
        {
            throw new DomainException("Coupon has not started.");
        }

        if (EndsAt is not null && at > EndsAt.Value)
        {
            throw new DomainException("Coupon has expired.");
        }

        if (UsageLimit is not null && UsedCount >= UsageLimit.Value)
        {
            throw new DomainException("Coupon usage limit has been reached.");
        }
    }

    public decimal CalculateDiscount(decimal eligibleSubtotal)
    {
        if (eligibleSubtotal <= 0m)
        {
            throw new DomainException("Coupon eligible subtotal must be greater than zero.");
        }

        if (MinimumSubtotal is not null && eligibleSubtotal < MinimumSubtotal.Value)
        {
            throw new DomainException("Coupon minimum subtotal has not been reached.");
        }

        var discount = DiscountType switch
        {
            CouponDiscountType.Percentage => eligibleSubtotal * DiscountValue / 100m,
            CouponDiscountType.FixedAmount => DiscountValue,
            _ => throw new DomainException("Coupon discount type is not supported.")
        };

        if (MaxDiscountAmount is not null)
        {
            discount = Math.Min(discount, MaxDiscountAmount.Value);
        }

        discount = Math.Min(discount, eligibleSubtotal);

        return Math.Round(discount, 2, MidpointRounding.AwayFromZero);
    }

    public void ReserveUsage()
    {
        if (UsageLimit is not null && UsedCount >= UsageLimit.Value)
        {
            throw new DomainException("Coupon usage limit has been reached.");
        }

        UsedCount++;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        return Guard.Required(code, nameof(Code)).ToUpperInvariant();
    }

    private static decimal ValidateDiscountValue(CouponDiscountType discountType, decimal value)
    {
        if (value <= 0m)
        {
            throw new DomainException("Coupon discount value must be greater than zero.");
        }

        if (discountType == CouponDiscountType.Percentage && value > 100m)
        {
            throw new DomainException("Coupon percentage discount cannot exceed 100.");
        }

        return value;
    }

    private static decimal? ValidateOptionalMoney(decimal? value, string name)
    {
        return value is null ? null : Guard.NotNegative(value.Value, name);
    }

    private static int? ValidateUsageLimit(int? usageLimit)
    {
        if (usageLimit is not null && usageLimit.Value <= 0)
        {
            throw new DomainException("Coupon usage limit must be greater than zero.");
        }

        return usageLimit;
    }

    private static void ValidateEffectiveWindow(DateTimeOffset? startsAt, DateTimeOffset? endsAt)
    {
        if (startsAt is not null && endsAt is not null && endsAt.Value <= startsAt.Value)
        {
            throw new DomainException("Coupon end time must be after start time.");
        }
    }
}
