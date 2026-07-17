using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutCouponApplier(
    ICheckoutStore checkoutStore,
    ICurrentCustomerContext currentCustomer)
{
    public async Task<Result<CheckoutCouponEvaluation>> EvaluateAsync(
        string? couponCode,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        bool lockCoupon,
        CancellationToken cancellationToken)
    {
        var normalizedCouponCode = NormalizeCouponCode(couponCode);
        if (normalizedCouponCode is null)
        {
            return Result<CheckoutCouponEvaluation>.Validation(["Coupon code is required."]);
        }

        var coupon = lockCoupon
            ? await checkoutStore.FindCouponByCodeForUpdateAsync(normalizedCouponCode, cancellationToken)
            : await checkoutStore.FindCouponByCodeAsync(normalizedCouponCode, cancellationToken);
        if (coupon is null)
        {
            return Result<CheckoutCouponEvaluation>.Validation(["Coupon was not found."]);
        }

        var availabilityResult = ValidateCouponAvailability(coupon, DateTimeOffset.UtcNow);
        if (availabilityResult.IsFailure)
        {
            return availabilityResult.Status == ResultStatus.Conflict
                ? Result<CheckoutCouponEvaluation>.Conflict(availabilityResult.FirstError ?? "A conflict occurred.")
                : Result<CheckoutCouponEvaluation>.Validation(availabilityResult.Errors);
        }

        var customerEligibilityResult = ValidateCouponCustomerEligibility(coupon, currentCustomer.CustomerId);
        if (customerEligibilityResult.IsFailure)
        {
            return Result<CheckoutCouponEvaluation>.Validation(customerEligibilityResult.Errors);
        }

        var productTargetIds = await checkoutStore.FindCouponProductTargetIdsAsync(coupon.Id, cancellationToken);
        var productTargetSet = productTargetIds.ToHashSet();
        var eligibleSubtotal = snapshots
            .Where(snapshot => productTargetSet.Count == 0 || productTargetSet.Contains(snapshot.ProductId))
            .Sum(snapshot => snapshot.LineTotal);
        if (eligibleSubtotal <= 0m)
        {
            return Result<CheckoutCouponEvaluation>.Validation(["Coupon does not apply to items in cart."]);
        }

        try
        {
            var discountAmount = coupon.CalculateDiscount(eligibleSubtotal);

            return Result<CheckoutCouponEvaluation>.Success(new CheckoutCouponEvaluation(
                coupon,
                eligibleSubtotal,
                snapshots.Sum(snapshot => snapshot.LineTotal),
                discountAmount));
        }
        catch (DomainException exception)
        {
            return Result<CheckoutCouponEvaluation>.Validation([exception.Message]);
        }
    }

    public static bool IsUsageLimitReached(Coupon coupon)
    {
        return coupon.UsageLimit is not null && coupon.UsedCount >= coupon.UsageLimit.Value;
    }

    public static string? NormalizeCouponCode(string? couponCode)
    {
        return string.IsNullOrWhiteSpace(couponCode)
            ? null
            : couponCode.Trim().ToUpperInvariant();
    }

    private static Result ValidateCouponAvailability(Coupon coupon, DateTimeOffset at)
    {
        if (!coupon.IsActive)
        {
            return Result.Validation(["Coupon is inactive."]);
        }

        if (coupon.StartsAt is not null && at < coupon.StartsAt.Value)
        {
            return Result.Validation(["Coupon has not started."]);
        }

        if (coupon.EndsAt is not null && at > coupon.EndsAt.Value)
        {
            return Result.Validation(["Coupon has expired."]);
        }

        if (IsUsageLimitReached(coupon))
        {
            return Result.Conflict("Coupon usage limit has been reached.");
        }

        return Result.Success();
    }

    private static Result ValidateCouponCustomerEligibility(Coupon coupon, Guid? customerId)
    {
        try
        {
            coupon.ValidateCustomerEligibility(customerId);

            return Result.Success();
        }
        catch (DomainException exception)
        {
            return Result.Validation([exception.Message]);
        }
    }
}

internal sealed record CheckoutCouponEvaluation(
    Coupon Coupon,
    decimal EligibleSubtotal,
    decimal Subtotal,
    decimal DiscountAmount);
