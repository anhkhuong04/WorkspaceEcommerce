using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.SessionId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(request => request.ProductVariantId)
            .NotEmpty();

        RuleFor(request => request.Quantity)
            .GreaterThan(0);
    }
}
