using FluentValidation;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutService(
    ICheckoutStore checkoutStore,
    ICurrentCustomerContext currentCustomer,
    ICurrentLanguageProvider languageProvider,
    IShipmentService shipmentService,
    IVNPayPaymentService vnPayPaymentService,
    ILogger<CheckoutService> logger,
    IValidator<CheckoutRequest> validator,
    IValidator<ValidateCheckoutCouponRequest> couponValidator) : ICheckoutService
{
    private readonly CheckoutCartBuilder cartBuilder = new(checkoutStore, languageProvider);
    private readonly CheckoutCouponApplier couponApplier = new(checkoutStore, currentCustomer);
    private readonly CheckoutOrderPlacer orderPlacer = new(checkoutStore, currentCustomer, languageProvider);
    private readonly CheckoutShippingCoordinator shippingCoordinator = new(shipmentService, checkoutStore, logger);

    public async Task<Result<GetShippingQuoteResponse>> GetShippingQuoteAsync(
        GetShippingQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionId = NormalizeSessionId(request.SessionId);
        var cart = await checkoutStore.FindCartBySessionIdAsync(sessionId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return Result<GetShippingQuoteResponse>.Validation(["Cart is empty."]);
        }

        var itemSnapshotsResult = await cartBuilder.BuildItemSnapshotsAsync(cart, cancellationToken);
        if (itemSnapshotsResult.IsFailure)
        {
            return itemSnapshotsResult.Status switch
            {
                ResultStatus.NotFound => Result<GetShippingQuoteResponse>.NotFound(itemSnapshotsResult.FirstError ?? "Resource was not found."),
                ResultStatus.Conflict => Result<GetShippingQuoteResponse>.Conflict(itemSnapshotsResult.FirstError ?? "A conflict occurred."),
                _ => Result<GetShippingQuoteResponse>.Validation(itemSnapshotsResult.Errors)
            };
        }

        return await shippingCoordinator.GetQuotePreviewAsync(
            request,
            itemSnapshotsResult.Value!,
            cancellationToken);
    }

    public async Task<Result<CheckoutCouponValidationResponse>> ValidateCouponAsync(
        ValidateCheckoutCouponRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await couponValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CheckoutCouponValidationResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var sessionId = NormalizeSessionId(request.SessionId);
        var cart = await checkoutStore.FindCartBySessionIdAsync(sessionId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return Result<CheckoutCouponValidationResponse>.Validation(["Cart is empty."]);
        }

        var itemSnapshotsResult = await cartBuilder.BuildItemSnapshotsAsync(cart, cancellationToken);
        if (itemSnapshotsResult.IsFailure)
        {
            return CheckoutResultMapper.ToCouponValidationFailure(itemSnapshotsResult);
        }

        var snapshots = itemSnapshotsResult.Value!;
        var evaluationResult = await couponApplier.EvaluateAsync(
            request.CouponCode,
            snapshots,
            lockCoupon: false,
            cancellationToken);
        if (evaluationResult.IsFailure)
        {
            return CheckoutResultMapper.ToCouponValidationFailure(evaluationResult);
        }

        var evaluation = evaluationResult.Value!;

        return Result<CheckoutCouponValidationResponse>.Success(new CheckoutCouponValidationResponse(
            evaluation.Coupon.Code,
            evaluation.DiscountAmount,
            evaluation.EligibleSubtotal,
            evaluation.Subtotal,
            evaluation.Subtotal - evaluation.DiscountAmount,
            "Coupon applied."));
    }

    public async Task<Result<CheckoutResponse>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CheckoutResponse>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var sessionId = NormalizeSessionId(request.SessionId);
        var cart = await checkoutStore.FindCartBySessionIdAsync(sessionId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return Result<CheckoutResponse>.Validation(["Cart is empty."]);
        }

        ShippingQuoteResponse? preCheckoutShippingQuote = null;

        if (request.PaymentMethod == PaymentMethod.VNPay)
        {
            var quoteSnapshotsResult = await cartBuilder.BuildItemSnapshotsAsync(cart, cancellationToken);
            if (quoteSnapshotsResult.IsFailure)
            {
                return CheckoutResultMapper.ToCheckoutFailure(quoteSnapshotsResult);
            }

            var quoteResult = await shippingCoordinator.GetQuoteForCheckoutAsync(
                request,
                quoteSnapshotsResult.Value!,
                codAmount: 0m,
                cancellationToken);
            if (quoteResult.IsFailure)
            {
                return CheckoutResultMapper.ToCheckoutFailure(quoteResult);
            }

            preCheckoutShippingQuote = quoteResult.Value!;
        }

        var placementResult = await orderPlacer.PlaceAsync(
            cart,
            request,
            preCheckoutShippingQuote,
            cancellationToken);
        if (placementResult.IsFailure)
        {
            return CheckoutResultMapper.ToCheckoutFailure(placementResult);
        }

        var placement = placementResult.Value!;
        string? paymentUrl = null;

        if (request.PaymentMethod == PaymentMethod.VNPay)
        {
            var paymentTransaction = placement.PaymentTransaction!;
            paymentUrl = vnPayPaymentService.CreatePaymentUrl(new VNPayCreatePaymentUrlRequest
            {
                TxnRef = paymentTransaction.TxnRef,
                Amount = paymentTransaction.Amount,
                OrderInfo = $"Pay order {placement.Order.OrderCode}",
                IpAddress = NormalizeClientIpAddress(request.ClientIpAddress)
            });
        }
        else
        {
            await shippingCoordinator.TryCreateShipmentAsync(
                placement.Order,
                request,
                placement.Snapshots,
                cancellationToken);
        }

        return Result<CheckoutResponse>.Success(new CheckoutResponse(
            CheckoutOrderMapper.ToDto(placement.Order),
            PaymentRequired: paymentUrl is not null,
            PaymentUrl: paymentUrl));
    }

    private static string NormalizeClientIpAddress(string? clientIpAddress)
    {
        return string.IsNullOrWhiteSpace(clientIpAddress)
            ? "127.0.0.1"
            : clientIpAddress.Trim();
    }

    private static string NormalizeSessionId(string sessionId)
    {
        return sessionId.Trim();
    }
}
