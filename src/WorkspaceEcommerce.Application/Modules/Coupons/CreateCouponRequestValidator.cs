using FluentValidation;
using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Application.Modules.Coupons;

public sealed class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(BeValidCode)
            .WithMessage("Code must use letters, numbers, underscores, or hyphens.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.Description)
            .MaximumLength(1000);

        RuleFor(request => request.DiscountType)
            .IsInEnum();

        RuleFor(request => request.DiscountValue)
            .GreaterThan(0);

        RuleFor(request => request.DiscountValue)
            .LessThanOrEqualTo(100)
            .When(request => request.DiscountType == CouponDiscountType.Percentage)
            .WithMessage("Percentage discount cannot exceed 100.");

        RuleFor(request => request.MaxDiscountAmount)
            .GreaterThanOrEqualTo(0)
            .When(request => request.MaxDiscountAmount.HasValue);

        RuleFor(request => request.MinimumSubtotal)
            .GreaterThanOrEqualTo(0)
            .When(request => request.MinimumSubtotal.HasValue);

        RuleFor(request => request.EndsAt)
            .GreaterThan(request => request.StartsAt)
            .When(request => request.StartsAt.HasValue && request.EndsAt.HasValue)
            .WithMessage("End time must be after start time.");

        RuleFor(request => request.UsageLimit)
            .GreaterThan(0)
            .When(request => request.UsageLimit.HasValue);

        RuleForEach(request => request.ProductTargetIds)
            .NotEmpty();

        RuleFor(request => request.ProductTargetIds)
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("Product target ids must be unique.");
    }

    private static bool BeValidCode(string code)
    {
        var normalizedCode = code.Trim();

        return normalizedCode.Length > 0 &&
            normalizedCode.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '_' or '-');
    }
}
