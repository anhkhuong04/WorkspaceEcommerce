using FluentValidation;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutService(
    ICheckoutStore checkoutStore,
    ICurrentCustomerContext currentCustomer,
    ICurrentLanguageProvider languageProvider,
    IShipmentService shipmentService,
    ILogger<CheckoutService> logger,
    IValidator<CheckoutRequest> validator,
    IValidator<ValidateCheckoutCouponRequest> couponValidator) : ICheckoutService
{
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

        var itemSnapshotsResult = await BuildItemSnapshotsAsync(cart, cancellationToken);
        if (itemSnapshotsResult.IsFailure)
        {
            return itemSnapshotsResult.Status switch
            {
                ResultStatus.NotFound => Result<GetShippingQuoteResponse>.NotFound(itemSnapshotsResult.FirstError ?? "Resource was not found."),
                ResultStatus.Conflict => Result<GetShippingQuoteResponse>.Conflict(itemSnapshotsResult.FirstError ?? "A conflict occurred."),
                _ => Result<GetShippingQuoteResponse>.Validation(itemSnapshotsResult.Errors)
            };
        }

        var snapshots = itemSnapshotsResult.Value!;
        var parcel = AggregateParcel(snapshots);
        var subtotal = snapshots.Sum(s => s.LineTotal);

        try
        {
            var quoteResponse = await shipmentService.GetShippingQuoteAsync(new ShippingQuoteRequest
            {
                DeliveryAddress = new ShippingAddress
                {
                    Street = request.Street,
                    Ward = request.Ward,
                    Province = request.Province
                },
                Parcel = parcel,
                GoodsValueAmount = subtotal,
                CodAmount = 0
            }, cancellationToken);

            return Result<GetShippingQuoteResponse>.Success(new GetShippingQuoteResponse(
                quoteResponse.TotalFeeAmount,
                quoteResponse.BaseFeeAmount,
                quoteResponse.ExtraWeightFeeAmount,
                quoteResponse.InsuranceFeeAmount,
                quoteResponse.RouteType,
                quoteResponse.Currency));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to get shipping quote from MiniLogistics");
            return Result<GetShippingQuoteResponse>.Failure("Could not calculate shipping fee. Please try again.");
        }
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

        var itemSnapshotsResult = await BuildItemSnapshotsAsync(cart, cancellationToken);
        if (itemSnapshotsResult.IsFailure)
        {
            return ToCouponValidationFailure(itemSnapshotsResult);
        }

        var snapshots = itemSnapshotsResult.Value!;
        var evaluationResult = await EvaluateCouponAsync(
            request.CouponCode,
            snapshots,
            lockCoupon: false,
            cancellationToken);
        if (evaluationResult.IsFailure)
        {
            return ToCouponValidationFailure(evaluationResult);
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

        Order? order = null;
        Result<CheckoutResponse>? failure = null;
        IReadOnlyCollection<CheckoutItemSnapshot>? snapshots = null;
        try
        {
            await checkoutStore.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var itemSnapshotsResult = await BuildItemSnapshotsAsync(cart, transactionCancellationToken);
                if (itemSnapshotsResult.IsFailure)
                {
                    failure = ToCheckoutFailure(itemSnapshotsResult);
                    return;
                }

                snapshots = itemSnapshotsResult.Value!;
                CheckoutCouponEvaluation? couponEvaluation = null;
                var normalizedCouponCode = NormalizeOptionalCouponCode(request.CouponCode);
                if (normalizedCouponCode is not null)
                {
                    var couponEvaluationResult = await EvaluateCouponAsync(
                        normalizedCouponCode,
                        snapshots,
                        lockCoupon: true,
                        transactionCancellationToken);
                    if (couponEvaluationResult.IsFailure)
                    {
                        failure = ToCheckoutFailure(couponEvaluationResult);
                        return;
                    }

                    couponEvaluation = couponEvaluationResult.Value!;
                }

                order = await CreateOrderAsync(
                    request,
                    currentCustomer.CustomerId,
                    snapshots,
                    transactionCancellationToken);

                if (couponEvaluation is not null)
                {
                    if (IsUsageLimitReached(couponEvaluation.Coupon))
                    {
                        failure = Result<CheckoutResponse>.Conflict("Coupon usage limit has been reached.");
                        return;
                    }

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
            return Result<CheckoutResponse>.Validation([exception.Message]);
        }

        if (failure is not null)
        {
            return failure;
        }

        // After order saved, calculate shipping fee and create shipment in MiniLogistics
        try
        {
            // Use saved item snapshots for parcel
            var parcelForShipment = AggregateParcel(snapshots!);

            // Use the structured address fields if available
            var deliveryAddress = new ShippingAddress
            {
                Street = !string.IsNullOrWhiteSpace(request.ShippingStreet) ? request.ShippingStreet : request.ShippingAddress,
                Ward = request.ShippingWard,
                Province = request.ShippingProvince
            };

            var isCod = request.PaymentMethod == PaymentMethod.Cod;
            var codAmount = isCod ? order!.TotalAmount : 0m;

            var quoteResponse = await shipmentService.GetShippingQuoteAsync(new ShippingQuoteRequest
            {
                ExternalOrderId = order!.OrderCode,
                DeliveryAddress = deliveryAddress,
                Parcel = parcelForShipment,
                GoodsValueAmount = order.Subtotal,
                CodAmount = codAmount
            }, cancellationToken);

            order.SetShippingFee(quoteResponse.TotalFeeAmount);

            var shipmentResponse = await shipmentService.CreateShipmentAsync(new CreateShipmentRequest
            {
                ExternalOrderId = order.OrderCode,
                Receiver = new ShipmentContact
                {
                    Name = order.CustomerName,
                    Phone = order.CustomerPhone
                },
                DeliveryAddress = deliveryAddress,
                Parcel = parcelForShipment,
                GoodsValueAmount = order.Subtotal,
                CodAmount = codAmount,
                Note = order.Note
            }, order.OrderCode, cancellationToken);

            order.UpdateShipmentInfo(shipmentResponse.TrackingCode, shipmentResponse.ShipmentId);

            checkoutStore.Update(order);
            await checkoutStore.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "MiniLogistics shipment creation failed for order {OrderCode}. Order was placed without shipment.", order!.OrderCode);
        }

        return Result<CheckoutResponse>.Success(new CheckoutResponse(ToDto(order!)));
    }

    private async Task<Result<IReadOnlyCollection<CheckoutItemSnapshot>>> BuildItemSnapshotsAsync(
        CartAggregate cart,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<CheckoutItemSnapshot>();

        foreach (var cartItem in cart.Items)
        {
            var variant = await checkoutStore.FindProductVariantByIdAsync(cartItem.ProductVariantId, cancellationToken);
            if (variant is null || !variant.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            var product = await checkoutStore.FindProductByIdAsync(variant.ProductId, cancellationToken);
            if (product is null || !product.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            var category = await checkoutStore.FindCategoryByIdAsync(product.CategoryId, cancellationToken);
            if (category is null || !category.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            if (cartItem.Quantity > variant.StockQuantity)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.Conflict("Requested quantity exceeds available stock.");
            }

            snapshots.Add(new CheckoutItemSnapshot(
                variant,
                product.Id,
                product.Name.Get(languageProvider.CurrentLanguage),
                variant.Sku,
                cartItem.UnitPriceSnapshot,
                cartItem.Quantity,
                variant.RequiresInstallation,
                variant.WeightKg,
                variant.LengthCm,
                variant.WidthCm,
                variant.HeightCm));
        }

        return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.Success(snapshots);
    }

    private async Task<Result<CheckoutCouponEvaluation>> EvaluateCouponAsync(
        string couponCode,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        bool lockCoupon,
        CancellationToken cancellationToken)
    {
        var normalizedCouponCode = NormalizeOptionalCouponCode(couponCode);
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

    private async Task<Order> CreateOrderAsync(
        CheckoutRequest request,
        Guid? customerId,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var shippingAddress = string.IsNullOrWhiteSpace(request.ShippingAddress)
            ? $"{request.ShippingStreet}, {request.ShippingWard}, {request.ShippingProvince}"
            : request.ShippingAddress;

        var currencyCode = languageProvider.CurrentLanguage == "vi" ? "VND" : "USD";
        var exchangeRate = languageProvider.CurrentLanguage == "vi" ? 26000m : 1m;

        var order = new Order(
            Guid.NewGuid(),
            await GenerateOrderCodeAsync(cancellationToken),
            customerId,
            request.CustomerName,
            request.CustomerPhone,
            request.CustomerEmail,
            shippingAddress,
            request.Note,
            request.PaymentMethod,
            currencyCode,
            exchangeRate);

        foreach (var snapshot in snapshots)
        {
            order.AddItem(
                Guid.NewGuid(),
                snapshot.Variant.Id,
                snapshot.ProductNameSnapshot,
                snapshot.SkuSnapshot,
                snapshot.UnitPrice,
                snapshot.Quantity,
                snapshot.RequiresInstallation);
        }

        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);

        return order;
    }

    private async Task<string> GenerateOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var orderCode = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
            if (!await checkoutStore.OrderCodeExistsAsync(orderCode, cancellationToken))
            {
                return orderCode;
            }
        }

        throw new DomainException("Could not generate a unique order code.");
    }

    private static OrderDto ToDto(Order order)
    {
        return new OrderDto(
            order.Id,
            order.OrderCode,
            order.CustomerId,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.ShippingAddress,
            order.Note,
            order.CouponId,
            order.CouponCodeSnapshot,
            order.CouponNameSnapshot,
            order.Subtotal,
            order.ShippingFee,
            order.DiscountAmount,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.CreatedAt,
            order.UpdatedAt,
            order.TrackingCode,
            order.ShipmentId,
            order.Items.Select(ToDto).ToArray());
    }

    private static OrderItemDto ToDto(OrderItem item)
    {
        return new OrderItemDto(
            item.Id,
            item.ProductVariantId,
            item.ProductNameSnapshot,
            item.SkuSnapshot,
            item.UnitPrice,
            item.Quantity,
            item.LineTotal,
            item.RequiresInstallation);
    }

    private static Result<CheckoutResponse> ToCheckoutFailure<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CheckoutResponse>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CheckoutResponse>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CheckoutResponse>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CheckoutResponse>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CheckoutResponse>.Failure(result.Errors)
        };
    }

    private static Result<CheckoutCouponValidationResponse> ToCouponValidationFailure<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CheckoutCouponValidationResponse>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CheckoutCouponValidationResponse>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CheckoutCouponValidationResponse>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CheckoutCouponValidationResponse>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CheckoutCouponValidationResponse>.Failure(result.Errors)
        };
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

    private static bool IsUsageLimitReached(Coupon coupon)
    {
        return coupon.UsageLimit is not null && coupon.UsedCount >= coupon.UsageLimit.Value;
    }

    private static string NormalizeSessionId(string sessionId)
    {
        return sessionId.Trim();
    }

    private static string? NormalizeOptionalCouponCode(string? couponCode)
    {
        return string.IsNullOrWhiteSpace(couponCode)
            ? null
            : couponCode.Trim().ToUpperInvariant();
    }

    private static ShippingParcel AggregateParcel(IReadOnlyCollection<CheckoutItemSnapshot> snapshots)
    {
        const decimal defaultWeightKg = 0.5m;
        const decimal defaultLengthCm = 15m;
        const decimal defaultWidthCm = 10m;
        const decimal defaultHeightCm = 8m;

        var totalWeight = 0m;
        var maxLength = 0m;
        var maxWidth = 0m;
        var totalHeight = 0m;

        foreach (var snapshot in snapshots)
        {
            var weight = snapshot.WeightKg ?? defaultWeightKg;
            var length = snapshot.LengthCm ?? defaultLengthCm;
            var width = snapshot.WidthCm ?? defaultWidthCm;
            var height = snapshot.HeightCm ?? defaultHeightCm;

            totalWeight += weight * snapshot.Quantity;
            if (length > maxLength) maxLength = length;
            if (width > maxWidth) maxWidth = width;
            totalHeight += height * snapshot.Quantity;
        }

        return new ShippingParcel
        {
            WeightKg = totalWeight,
            LengthCm = maxLength,
            WidthCm = maxWidth,
            HeightCm = totalHeight
        };
    }

    private sealed record CheckoutItemSnapshot(
        ProductVariant Variant,
        Guid ProductId,
        string ProductNameSnapshot,
        string SkuSnapshot,
        decimal UnitPrice,
        int Quantity,
        bool RequiresInstallation,
        decimal? WeightKg,
        decimal? LengthCm,
        decimal? WidthCm,
        decimal? HeightCm)
    {
        public decimal LineTotal => UnitPrice * Quantity;
    }

    private sealed record CheckoutCouponEvaluation(
        Coupon Coupon,
        decimal EligibleSubtotal,
        decimal Subtotal,
        decimal DiscountAmount);
}
