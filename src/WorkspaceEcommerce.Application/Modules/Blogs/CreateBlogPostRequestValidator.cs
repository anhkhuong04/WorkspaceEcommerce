using System.Linq;
using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed class CreateBlogPostRequestValidator : AbstractValidator<CreateBlogPostRequest>
{
    public CreateBlogPostRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.Slug)
            .NotEmpty()
            .MaximumLength(250)
            .Must(BeValidSlug)
            .WithMessage("Slug must contain only alphanumeric characters, hyphens, or underscores.");

        RuleFor(request => request.Summary)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(request => request.Content)
            .NotEmpty();

        RuleFor(request => request.ImageUrl)
            .MaximumLength(1000)
            .When(request => !string.IsNullOrEmpty(request.ImageUrl));

        RuleFor(request => request.RelatedProductIds)
            .Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Related product IDs must be unique.");
    }

    private static bool BeValidSlug(string slug)
    {
        var normalizedSlug = slug.Trim();
        if (normalizedSlug.Length == 0)
        {
            return false;
        }

        return normalizedSlug.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '_' or '-');
    }
}
