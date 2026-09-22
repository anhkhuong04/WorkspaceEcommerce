using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed class CreateProductVariantRequestValidator : AbstractValidator<CreateProductVariantRequest>
{
    public CreateProductVariantRequestValidator()
    {
        RuleFor(request => request.Sku)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("SKU must use letters, numbers, dots, underscores, or hyphens.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.Color)
            .MaximumLength(100);

        RuleFor(request => request.Size)
            .MaximumLength(100);

        RuleFor(request => request.Price)
            .GreaterThanOrEqualTo(0)
            .Must(price => price == decimal.Truncate(price))
            .WithMessage("Price must be a whole VND amount.");

        RuleFor(request => request.CompareAtPrice)
            .GreaterThanOrEqualTo(request => request.Price)
            .Must(price => price == decimal.Truncate(price!.Value))
            .When(request => request.CompareAtPrice is not null)
            .WithMessage("Compare-at price must be a whole VND amount and cannot be lower than price.");

        RuleFor(request => request.StockQuantity)
            .GreaterThanOrEqualTo(0);
    }
}
