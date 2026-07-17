using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutOrderPlacer(
    ICheckoutStore checkoutStore,
    ICurrentCustomerContext currentCustomer,
    ICurrentLanguageProvider languageProvider)
{
    private readonly CheckoutCartBuilder cartBuilder = new(checkoutStore, languageProvider);
    private readonly CheckoutCouponApplier couponApplier = new(checkoutStore, currentCustomer);
    private readonly CheckoutOrderFactory orderFactory = new(checkoutStore, languageProvider);

    public async Task<Result<CheckoutPlacement>> PlaceAsync(
        CartAggregate cart,
        CheckoutRequest request,
        ShippingQuoteResponse? preCheckoutShippingQuote,
        CancellationToken cancellationToken)
    {
        Order? order = null;
        PaymentTransaction? paymentTransaction = null;
        IReadOnlyCollection<CheckoutItemSnapshot>? snapshots = null;
        Result<CheckoutPlacement>? failure = null;

        try
        {
            await checkoutStore.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var itemSnapshotsResult = await cartBuilder.BuildItemSnapshotsAsync(cart, transactionCancellationToken);
                if (itemSnapshotsResult.IsFailure)
                {
                    failure = ToPlacementFailure(itemSnapshotsResult);
                    return;
                }

                snapshots = itemSnapshotsResult.Value!;
                var couponEvaluationResult = await EvaluateCouponAsync(request, snapshots, transactionCancellationToken);
                if (couponEvaluationResult.IsFailure)
                {
                    failure = ToPlacementFailure(couponEvaluationResult);
                    return;
                }

                order = await orderFactory.CreateAsync(
                    request,
                    currentCustomer.CustomerId,
                    snapshots,
                    transactionCancellationToken);

                var couponEvaluation = couponEvaluationResult.Value;
                if (couponEvaluation is not null)
                {
                    if (CheckoutCouponApplier.IsUsageLimitReached(couponEvaluation.Coupon))
                    {
                        failure = Result<CheckoutPlacement>.Conflict("Coupon usage limit has been reached.");
                        return;
                    }

                    ApplyCoupon(order, couponEvaluation);
                }

                if (preCheckoutShippingQuote is not null)
                {
                    order.SetShippingFee(preCheckoutShippingQuote.TotalFeeAmount);
                }

                if (request.PaymentMethod == PaymentMethod.VNPay)
                {
                    paymentTransaction = new PaymentTransaction(
                        Guid.NewGuid(),
                        order.Id,
                        PaymentProvider.VNPay,
                        order.TotalAmount,
                        order.CurrencyCode,
                        GeneratePaymentTxnRef(order));
                    checkoutStore.Add(paymentTransaction);
                }

                foreach (var snapshot in snapshots)
                {
                    snapshot.Variant.DecreaseStock(snapshot.Quantity);
                    checkoutStore.Update(snapshot.Variant);
                }

                checkoutStore.Add(order);
                checkoutStore.Remove(cart);
                await checkoutStore.SaveChangesAsync(transactionCancellationToken);
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<CheckoutPlacement>.Validation([exception.Message]);
        }

        if (failure is not null)
        {
            return failure;
        }

        return Result<CheckoutPlacement>.Success(new CheckoutPlacement(
            order!,
            paymentTransaction,
            snapshots!));
    }

    private async Task<Result<CheckoutCouponEvaluation?>> EvaluateCouponAsync(
        CheckoutRequest request,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var normalizedCouponCode = CheckoutCouponApplier.NormalizeCouponCode(request.CouponCode);
        if (normalizedCouponCode is null)
        {
            return Result<CheckoutCouponEvaluation?>.Success(null);
        }

        var couponEvaluationResult = await couponApplier.EvaluateAsync(
            normalizedCouponCode,
            snapshots,
            lockCoupon: true,
            cancellationToken);

        return couponEvaluationResult.IsFailure
            ? ToNullableCouponFailure(couponEvaluationResult)
            : Result<CheckoutCouponEvaluation?>.Success(couponEvaluationResult.Value!);
    }

    private void ApplyCoupon(Order order, CheckoutCouponEvaluation couponEvaluation)
    {
        order.ApplyCoupon(
            couponEvaluation.Coupon.Id,
            couponEvaluation.Coupon.Code,
            couponEvaluation.Coupon.Name,
            couponEvaluation.DiscountAmount);
        couponEvaluation.Coupon.ReserveUsage();
        checkoutStore.Update(couponEvaluation.Coupon);
        checkoutStore.Add(new CouponRedemption(
            Guid.NewGuid(),
            couponEvaluation.Coupon.Id,
            order.Id,
            currentCustomer.CustomerId,
            order.CustomerPhone,
            couponEvaluation.Coupon.Code,
            couponEvaluation.DiscountAmount));
    }

    private static Result<CheckoutPlacement> ToPlacementFailure<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CheckoutPlacement>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CheckoutPlacement>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CheckoutPlacement>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CheckoutPlacement>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CheckoutPlacement>.Failure(result.Errors)
        };
    }

    private static Result<CheckoutCouponEvaluation?> ToNullableCouponFailure(
        Result<CheckoutCouponEvaluation> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CheckoutCouponEvaluation?>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CheckoutCouponEvaluation?>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CheckoutCouponEvaluation?>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CheckoutCouponEvaluation?>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CheckoutCouponEvaluation?>.Failure(result.Errors)
        };
    }

    private static string GeneratePaymentTxnRef(Order order)
    {
        return $"{order.OrderCode}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();
    }
}

internal sealed record CheckoutPlacement(
    Order Order,
    PaymentTransaction? PaymentTransaction,
    IReadOnlyCollection<CheckoutItemSnapshot> Snapshots);
