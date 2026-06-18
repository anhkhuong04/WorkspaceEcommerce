using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class ValidateCheckoutCouponRequestValidator : AbstractValidator<ValidateCheckoutCouponRequest>
{
    public ValidateCheckoutCouponRequestValidator()
    {
        RuleFor(request => request.SessionId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(request => request.CouponCode)
            .NotEmpty()
            .MaximumLength(50)
            .Must(BeValidCouponCode)
            .WithMessage("Coupon code must use letters, numbers, underscores, or hyphens.");
    }

    private static bool BeValidCouponCode(string code)
    {
        var normalizedCode = code.Trim();

        return normalizedCode.Length > 0 &&
            normalizedCode.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '_' or '-');
    }
}
