using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed class ShippingCheckoutOptions
{
    public const string SectionName = "ShippingCheckout";
    public bool Enabled { get; set; }
    public bool WorkspaceEnabled { get; set; } = true;
    public bool ExternalEnabled { get; set; } = true;
    public int QuoteLifetimeMinutes { get; set; } = 10;
    public int UnusedQuoteRetentionDays { get; set; } = 30;
    public int ExternalQuoteTimeoutSeconds { get; set; } = 12;
}
public sealed record ShippingCheckoutCapabilities(bool Enabled, bool WorkspaceEnabled, bool ExternalEnabled);
public sealed record ShippingQuoteInput
{
    public string SessionId { get; init; } = string.Empty;
    public string ShippingStreet { get; init; } = string.Empty;
    public string ProvinceCode { get; init; } = string.Empty;
    public string WardCode { get; init; } = string.Empty;
    public PaymentMethod PaymentMethod { get; init; }
    public string? CouponCode { get; init; }
    public ShippingMethod Method { get; init; }
}
public sealed class ShippingQuoteInputValidator : AbstractValidator<ShippingQuoteInput>
{
    public ShippingQuoteInputValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ShippingStreet).NotEmpty().MaximumLength(250);
        RuleFor(x => x.ProvinceCode).NotEmpty().Matches("^[0-9]{2}$");
        RuleFor(x => x.WardCode).NotEmpty().Matches("^[0-9]{5}$");
        RuleFor(x => x.PaymentMethod).IsInEnum().Must(x => x is PaymentMethod.Cod or PaymentMethod.ManualBankTransfer or PaymentMethod.VNPay);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.CouponCode).MaximumLength(50);
    }
}
public sealed record ShippingMethodOption(ShippingMethod Method, bool Available, string? Reason);
public sealed record ShippingQuoteDto(Guid Id, ShippingMethod Method, decimal CustomerCharge, string Currency,
    int WeightGrams, DateTimeOffset ExpiresAtUtc, Guid? RatePlanId);
public sealed record RevokeShippingQuoteRequest(string Reason);
public interface IShippingQuoteService
{
    Task<Result<IReadOnlyCollection<ShippingMethodOption>>> OptionsAsync(ShippingQuoteInput input, CancellationToken cancellationToken);
    Task<Result<ShippingQuoteDto>> IssueAsync(ShippingQuoteInput input, CancellationToken cancellationToken);
    Task<Result> RevokeAsync(Guid id, string actor, string reason, CancellationToken cancellationToken);
}
internal sealed record ShippingInputSnapshot(int Version, ShippingDestination Destination, string Street,
    ShippingParcel Parcel, decimal GoodsValue, decimal Discount, PaymentMethod PaymentMethod,
    string? CouponCode, string? ZoneCode, long? RateRevision, ShippingRateCalculation? Calculation,
    ShippingQuoteResponse? ProviderQuote);
