using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Application.Modules.Coupons;

public sealed record AdminCouponDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    CouponDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal? MinimumSubtotal,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int? UsageLimit,
    Guid? CustomerId,
    CouponSource Source,
    int UsedCount,
    int RedemptionCount,
    bool IsActive,
    IReadOnlyCollection<Guid> ProductTargetIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
