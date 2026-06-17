using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

public sealed class UpdateCustomerProfileRequestValidator : AbstractValidator<UpdateCustomerProfileRequest>
{
    public UpdateCustomerProfileRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.PhoneNumber)
            .NotEmpty()
            .MaximumLength(50);
    }
}
