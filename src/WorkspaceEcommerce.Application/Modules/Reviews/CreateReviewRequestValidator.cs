using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Reviews;

internal sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(request => request.Slug)
            .NotEmpty().WithMessage("Product slug is required.");

        RuleFor(request => request.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");

        RuleFor(request => request.Comment)
            .MaximumLength(2000).WithMessage("Comment must not exceed 2000 characters.")
            .When(request => request.Comment is not null);
    }
}
