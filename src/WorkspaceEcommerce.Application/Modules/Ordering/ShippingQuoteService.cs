using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class ShippingQuoteService(ICheckoutStore checkout, IShippingCheckoutStore store,
    IShippingRateStore rates, IShippingAdministrativeCatalog catalog, ICurrentCustomerContext customer,
    ICurrentLanguageProvider language, IShipmentService provider, TimeProvider clock,
    IOptions<ShippingCheckoutOptions> options, IValidator<ShippingQuoteInput> validator) : IShippingQuoteService
{
    private readonly CheckoutCartBuilder builder = new(checkout, language);
    private readonly CheckoutCouponApplier coupons = new(checkout, customer);
    public async Task<Result<IReadOnlyCollection<ShippingMethodOption>>> OptionsAsync(ShippingQuoteInput input, CancellationToken cancellationToken)
    {
        var context = await ContextAsync(input, cancellationToken);
        if (context.IsFailure) return Result<IReadOnlyCollection<ShippingMethodOption>>.Validation(context.Errors);
        var local = await LocalAsync(context.Value!.Destination, context.Value.Items, cancellationToken);
        return Result<IReadOnlyCollection<ShippingMethodOption>>.Success([
            new(ShippingMethod.WorkspaceEC, options.Value.WorkspaceEnabled && local.IsSuccess,
                !options.Value.WorkspaceEnabled ? "MethodDisabled" : local.IsFailure ? local.FirstError : null),
            new(ShippingMethod.MiniLogistics, options.Value.ExternalEnabled, options.Value.ExternalEnabled ? "QuoteRequired" : "MethodDisabled")]);
    }
    public async Task<Result<ShippingQuoteDto>> IssueAsync(ShippingQuoteInput input, CancellationToken cancellationToken)
    {
        var context = await ContextAsync(input, cancellationToken);
        if (context.IsFailure) return Result<ShippingQuoteDto>.Validation(context.Errors);
        var (destination, items, discount) = context.Value!;
        var settings = options.Value;
        if (input.Method == ShippingMethod.WorkspaceEC && !settings.WorkspaceEnabled || input.Method == ShippingMethod.MiniLogistics && !settings.ExternalEnabled)
            return Result<ShippingQuoteDto>.Conflict("Shipping method is not accepting new quotes.");
        ShippingRateCalculation? calculation = null;
        ShippingQuoteResponse? providerQuote = null;
        Guid? planId = null;
        long? revision = null;
        string? zoneCode = null;
        var parcel = CheckoutCartBuilder.AggregateParcel(items);
        decimal charge;
        int grams;
        if (input.Method == ShippingMethod.WorkspaceEC)
        {
            var local = await LocalAsync(destination, items, cancellationToken);
            if (local.IsFailure) return Result<ShippingQuoteDto>.Conflict(local.FirstError!);
            (var plan, var zone, calculation) = local.Value!;
            planId = plan.Id; revision = plan.Revision; zoneCode = zone.Code;
            charge = calculation.TotalFee; grams = calculation.WeightGrams;
            // Local weight is actual, rounded once. Legacy dimensional defaults
            // remain only parcel metadata; they do not affect WorkspaceEC fees.
            parcel = new ShippingParcel { WeightKg = grams / 1000m, LengthCm = parcel.LengthCm, WidthCm = parcel.WidthCm, HeightCm = parcel.HeightCm };
        }
        else
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.ExternalQuoteTimeoutSeconds));
            try
            {
                providerQuote = await provider.GetShippingQuoteAsync(new ShippingQuoteRequest
                {
                    DeliveryAddress = new ShippingAddress { Street = input.ShippingStreet.Trim(), Ward = destination.WardName, Province = destination.ProvinceName },
                    Parcel = parcel, GoodsValueAmount = items.Sum(x => x.LineTotal),
                    CodAmount = input.PaymentMethod == Domain.Modules.Ordering.PaymentMethod.Cod ? items.Sum(x => x.LineTotal) - discount : 0
                }, timeout.Token);
                if (providerQuote.Currency != "VND") return Result<ShippingQuoteDto>.Conflict("Unsupported provider quote currency.");
                ShippingRateZone.ValidateMoney(providerQuote.TotalFeeAmount);
                charge = providerQuote.TotalFeeAmount;
                grams = checked((int)decimal.Ceiling(parcel.WeightKg * 1000));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Result<ShippingQuoteDto>.Conflict("Shipping provider is temporarily unavailable."); }
            catch (Exception exception) when (exception is HttpRequestException or DomainException or OverflowException or InvalidOperationException)
            { return Result<ShippingQuoteDto>.Conflict("Shipping provider is temporarily unavailable."); }
        }
        var snapshot = new ShippingInputSnapshot(1, destination, input.ShippingStreet.Trim(), parcel, items.Sum(x => x.LineTotal), discount,
            input.PaymentMethod, CheckoutCouponApplier.NormalizeCouponCode(input.CouponCode), zoneCode, revision, calculation, providerQuote);
        var now = clock.GetUtcNow();
        var quote = new ShippingQuote(Guid.NewGuid(), input.Method, ShippingQuoteFingerprint.Subject(customer.CustomerId, input.SessionId),
            ShippingQuoteFingerprint.Inputs(input, destination.CatalogVersion, discount, items), charge, grams,
            JsonSerializer.Serialize(snapshot), planId, now, now.AddMinutes(settings.QuoteLifetimeMinutes));
        checkout.Add(quote);
        await checkout.SaveChangesAsync(cancellationToken);
        return Result<ShippingQuoteDto>.Success(new(quote.Id, quote.Method, charge, "VND", grams, quote.ExpiresAtUtc, planId));
    }
    public Task<Result> RevokeAsync(Guid id, string actor, string reason, CancellationToken cancellationToken) => store.TransactionAsync(async ct =>
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || string.IsNullOrWhiteSpace(actor) || actor.Length > 256)
            return Result.Validation(["An authenticated actor and a reason of at most 500 characters are required."]);
        var quote = await store.LockQuoteAsync(id, ct);
        if (quote is null) return Result.NotFound("Quote was not found.");
        if (quote.OrderId.HasValue) return Result.Conflict("Accepted quotes cannot be revoked.");
        if (quote.RevokedAtUtc.HasValue) return Result.Success();
        quote.Revoke(clock.GetUtcNow(), actor, reason.Trim());
        await checkout.SaveChangesAsync(ct);
        return Result.Success();
    }, cancellationToken);

    private async Task<Result<QuoteContext>> ContextAsync(ShippingQuoteInput input, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Result<QuoteContext>.Validation(["Shipping checkout is not enabled."]);
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid) return Result<QuoteContext>.Validation(validation.Errors.Select(x => x.ErrorMessage));
        var destination = catalog.Resolve(input.ProvinceCode, input.WardCode);
        if (destination is null) return Result<QuoteContext>.Validation(["Province and ward codes do not match the administrative catalog."]);
        var cart = await checkout.FindCartBySessionIdAsync(input.SessionId.Trim(), ct);
        if (cart is null || cart.Items.Count == 0 || cart.CustomerId.HasValue && cart.CustomerId != customer.CustomerId)
            return Result<QuoteContext>.Validation(["Cart is unavailable."]);
        var items = await builder.BuildItemSnapshotsAsync(cart, false, ct);
        if (items.IsFailure) return Result<QuoteContext>.Validation(items.Errors);
        decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(input.CouponCode))
        {
            var evaluation = await coupons.EvaluateAsync(input.CouponCode, items.Value!, false, ct);
            if (evaluation.IsFailure) return Result<QuoteContext>.Validation(evaluation.Errors);
            discount = evaluation.Value!.DiscountAmount;
        }
        return Result<QuoteContext>.Success(new(destination, items.Value!, discount));
    }
    private async Task<Result<(ShippingRatePlan Plan, ShippingRateZone Zone, ShippingRateCalculation Calculation)>> LocalAsync(
        ShippingDestination destination, IReadOnlyCollection<CheckoutItemSnapshot> items, CancellationToken ct)
    {
        var planId = await rates.Plans.Where(x => x.Status == ShippingRatePlanStatus.Published).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var plan = planId.HasValue ? await rates.FindAsync(planId.Value, ct) : null;
        if (plan is null || plan.CatalogVersion != destination.CatalogVersion)
            return Result<(ShippingRatePlan, ShippingRateZone, ShippingRateCalculation)>.Conflict("No compatible published WorkspaceEC rates.");
        var zone = plan.Zones.SingleOrDefault(x => x.Provinces.Any(p => p.ProvinceCode == destination.ProvinceCode));
        if (zone is null) return Result<(ShippingRatePlan, ShippingRateZone, ShippingRateCalculation)>.Conflict("Destination is not covered.");
        var calculation = WorkspaceShippingRateCalculator.Calculate(zone, items.Select(x => new ShippingWeightLine(x.WeightKg, x.Quantity)).ToArray());
        return calculation.IsSuccess ? Result<(ShippingRatePlan, ShippingRateZone, ShippingRateCalculation)>.Success((plan, zone, calculation.Value!))
            : Result<(ShippingRatePlan, ShippingRateZone, ShippingRateCalculation)>.Conflict(calculation.FirstError!);
    }
    private sealed record QuoteContext(ShippingDestination Destination, IReadOnlyCollection<CheckoutItemSnapshot> Items, decimal Discount);
}
