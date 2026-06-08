using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum();

        RuleFor(request => request.Note)
            .MaximumLength(1000);
    }
}
