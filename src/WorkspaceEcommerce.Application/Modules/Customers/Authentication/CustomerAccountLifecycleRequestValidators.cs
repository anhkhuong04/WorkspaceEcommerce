using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed class RequestEmailVerificationRequestValidator : AbstractValidator<RequestEmailVerificationRequest>
{
    public RequestEmailVerificationRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(250);
    }
}

public sealed class ConfirmEmailVerificationRequestValidator : AbstractValidator<ConfirmEmailVerificationRequest>
{
    public ConfirmEmailVerificationRequestValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MinimumLength(32).MaximumLength(256);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(250);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MinimumLength(32).MaximumLength(256);
        RuleFor(request => request.NewPassword).MinimumLength(8).MaximumLength(128);
    }
}
