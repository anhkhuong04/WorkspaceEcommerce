using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;

public sealed class ConfirmTwoFactorSetupRequestValidator : AbstractValidator<ConfirmTwoFactorSetupRequest>
{
    public ConfirmTwoFactorSetupRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("Authentication code must contain exactly 6 digits.");
    }
}

public sealed class DisableTwoFactorRequestValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorRequestValidator()
    {
        RuleFor(request => request)
            .Must(request =>
                (!string.IsNullOrWhiteSpace(request.Code) && string.IsNullOrWhiteSpace(request.RecoveryCode)) ||
                (string.IsNullOrWhiteSpace(request.Code) && !string.IsNullOrWhiteSpace(request.RecoveryCode)))
            .WithMessage("Provide exactly one authentication code or recovery code.");

        When(request => !string.IsNullOrWhiteSpace(request.Code), () =>
        {
            RuleFor(request => request.Code!)
                .Matches("^[0-9]{6}$")
                .WithMessage("Authentication code must contain exactly 6 digits.");
        });

        When(request => !string.IsNullOrWhiteSpace(request.RecoveryCode), () =>
        {
            RuleFor(request => request.RecoveryCode!)
                .MaximumLength(128);
        });
    }
}

public sealed class VerifyTwoFactorLoginRequestValidator : AbstractValidator<VerifyTwoFactorLoginRequest>
{
    public VerifyTwoFactorLoginRequestValidator()
    {
        RuleFor(request => request.ChallengeToken).NotEmpty().MaximumLength(256);
        RuleFor(request => request.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("Authentication code must contain exactly 6 digits.");
    }
}

public sealed class VerifyTwoFactorRecoveryRequestValidator : AbstractValidator<VerifyTwoFactorRecoveryRequest>
{
    public VerifyTwoFactorRecoveryRequestValidator()
    {
        RuleFor(request => request.ChallengeToken).NotEmpty().MaximumLength(256);
        RuleFor(request => request.RecoveryCode).NotEmpty().MaximumLength(128);
    }
}
