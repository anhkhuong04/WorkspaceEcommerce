using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Categories;

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.Slug)
            .NotEmpty()
            .MaximumLength(200)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must use lowercase letters, numbers, and hyphens.");

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}
