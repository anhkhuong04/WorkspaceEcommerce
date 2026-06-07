using FluentValidation;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(request => request.SessionId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(request => request.CustomerName)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.CustomerPhone)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.CustomerEmail)
            .EmailAddress()
            .MaximumLength(250)
            .When(request => !string.IsNullOrWhiteSpace(request.CustomerEmail));

        RuleFor(request => request.ShippingAddress)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(request => request.Note)
            .MaximumLength(1000);

        RuleFor(request => request.PaymentMethod)
            .IsInEnum()
            .Must(method => method is PaymentMethod.Cod or PaymentMethod.ManualBankTransfer)
            .WithMessage("Payment method is not supported.");
    }
}
