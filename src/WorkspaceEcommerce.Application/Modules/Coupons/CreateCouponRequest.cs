using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Application.Modules.Coupons;

public sealed class CreateCouponRequest
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public CouponDiscountType DiscountType { get; init; }

    public decimal DiscountValue { get; init; }

    public decimal? MaxDiscountAmount { get; init; }

    public decimal? MinimumSubtotal { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public int? UsageLimit { get; init; }

    public bool IsActive { get; init; } = true;

    public Guid[] ProductTargetIds { get; init; } = [];
}
