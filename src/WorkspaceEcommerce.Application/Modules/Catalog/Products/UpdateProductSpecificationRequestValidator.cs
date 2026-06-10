using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed class UpdateProductSpecificationRequestValidator : AbstractValidator<UpdateProductSpecificationRequest>
{
    public UpdateProductSpecificationRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Value)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
