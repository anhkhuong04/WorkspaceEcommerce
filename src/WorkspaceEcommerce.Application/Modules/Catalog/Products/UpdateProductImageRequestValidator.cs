using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed class UpdateProductImageRequestValidator : AbstractValidator<UpdateProductImageRequest>
{
    public UpdateProductImageRequestValidator()
    {
        RuleFor(request => request.ImageUrl)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(request => request.AltText)
            .MaximumLength(250);
    }
}
