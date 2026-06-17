using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed class CustomerRegisterRequestValidator : AbstractValidator<CustomerRegisterRequest>
{
    public CustomerRegisterRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.PhoneNumber)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(250);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100);
    }
}
