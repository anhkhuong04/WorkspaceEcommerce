using FluentValidation;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(request => request.SessionId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(request => request.CustomerName)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.CustomerPhone)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.CustomerEmail)
            .EmailAddress()
            .MaximumLength(250)
            .When(request => !string.IsNullOrWhiteSpace(request.CustomerEmail));

        RuleFor(request => request.ShippingStreet)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.ShippingWard)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.ShippingProvince)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.Note)
            .MaximumLength(1000);

        RuleFor(request => request.CouponCode)
            .MaximumLength(50)
            .Must(BeValidCouponCode)
            .WithMessage("Coupon code must use letters, numbers, underscores, or hyphens.")
            .When(request => !string.IsNullOrWhiteSpace(request.CouponCode));

        RuleFor(request => request.PaymentMethod)
            .IsInEnum()
            .Must(method => method is PaymentMethod.Cod or PaymentMethod.ManualBankTransfer)
            .WithMessage("Payment method is not supported.");
    }

    private static bool BeValidCouponCode(string? code)
    {
        var normalizedCode = code?.Trim();

        return !string.IsNullOrEmpty(normalizedCode) &&
            normalizedCode.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '_' or '-');
    }
}
