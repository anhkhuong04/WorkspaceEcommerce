using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed class UpdateProductVariantRequestValidator : AbstractValidator<UpdateProductVariantRequest>
{
    public UpdateProductVariantRequestValidator()
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

        RuleFor(request => request.WeightKg).GreaterThan(0m).LessThanOrEqualTo(20000m)
            .When(request => request.WeightKg.HasValue);

        RuleFor(request => request.LengthCm).GreaterThan(0m).LessThanOrEqualTo(20000m)
            .When(request => request.LengthCm.HasValue);

        RuleFor(request => request.WidthCm).GreaterThan(0m).LessThanOrEqualTo(20000m)
            .When(request => request.WidthCm.HasValue);

        RuleFor(request => request.HeightCm).GreaterThan(0m).LessThanOrEqualTo(20000m)
            .When(request => request.HeightCm.HasValue);

        RuleFor(request => request.StockQuantity)
            .GreaterThanOrEqualTo(0);
    }
}
