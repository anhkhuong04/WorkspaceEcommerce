using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class RemoveCartItemRequestValidator : AbstractValidator<RemoveCartItemRequest>
{
    public RemoveCartItemRequestValidator()
    {
        RuleFor(request => request.SessionId)
            .NotEmpty()
            .MaximumLength(128);
    }
}
