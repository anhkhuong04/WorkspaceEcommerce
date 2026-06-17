using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed class CustomerLoginRequestValidator : AbstractValidator<CustomerLoginRequest>
{
    public CustomerLoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(250);

        RuleFor(request => request.Password)
            .NotEmpty();
    }
}
