using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class GetCartRequestValidator : AbstractValidator<GetCartRequest>
{
    public GetCartRequestValidator()
    {
        RuleFor(request => request.SessionId)
            .NotEmpty()
            .MaximumLength(128);
    }
}
