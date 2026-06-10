using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed class CreateProductImageRequestValidator : AbstractValidator<CreateProductImageRequest>
{
    public CreateProductImageRequestValidator()
    {
        RuleFor(request => request.ImageUrl)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(request => request.AltText)
            .MaximumLength(250);
    }
}
