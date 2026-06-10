using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed class CreateProductSpecificationRequestValidator : AbstractValidator<CreateProductSpecificationRequest>
{
    public CreateProductSpecificationRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Value)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
