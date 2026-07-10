using WorkspaceEcommerce.Domain.Modules.Loyalty;

namespace WorkspaceEcommerce.Application.Modules.Loyalty;

public sealed record LoyaltyAccountDto(
    Guid? AccountId,
    Guid CustomerId,
    int CurrentPoints,
    int TotalPointsEarned,
    LoyaltyTierType CurrentTier,
    decimal DiscountPercent,
    bool FreeShippingEnabled,
    LoyaltyTierType? NextTier,
    int? PointsToNextTier);

public sealed record LoyaltyTransactionDto(
    Guid Id,
    LoyaltyTransactionType Type,
    int Points,
    int BalanceAfter,
    Guid? OrderId,
    Guid? VoucherId,
    string Description,
    DateTimeOffset CreatedAt);

public sealed record LoyaltyTierDto(
    Guid Id,
    LoyaltyTierType Type,
    int MinTotalPointsEarned,
    decimal DiscountPercent,
    bool FreeShippingEnabled);

public sealed record RedeemLoyaltyPointsResponse(
    Guid VoucherId,
    string VoucherCode,
    decimal DiscountAmount,
    int RemainingPoints,
    DateTimeOffset ExpiresAt);
