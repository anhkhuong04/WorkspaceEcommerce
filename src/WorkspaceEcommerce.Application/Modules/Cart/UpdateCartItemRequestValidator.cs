using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(request => request.SessionId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(request => request.Quantity)
            .GreaterThan(0);
    }
}
