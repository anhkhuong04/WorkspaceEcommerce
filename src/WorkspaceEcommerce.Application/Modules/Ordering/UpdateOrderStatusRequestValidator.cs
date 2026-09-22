using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum();

        RuleFor(request => request.InternalNote)
            .MaximumLength(1000);

        RuleFor(request => request.CancellationReason)
            .NotEmpty()
            .When(request => request.Status == Domain.Modules.Ordering.OrderStatus.Cancelled)
            .WithMessage("Cancellation reason is required when cancelling an order.")
            .MaximumLength(500);

        RuleFor(request => request.CancellationReason)
            .Empty()
            .When(request => request.Status != Domain.Modules.Ordering.OrderStatus.Cancelled)
            .WithMessage("Cancellation reason can only be supplied when cancelling an order.");

        RuleFor(request => request.CustomerMessage)
            .MaximumLength(1000);
    }
}
