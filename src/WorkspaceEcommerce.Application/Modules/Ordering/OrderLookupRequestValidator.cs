using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class OrderLookupRequestValidator : AbstractValidator<OrderLookupRequest>
{
    public OrderLookupRequestValidator()
    {
        RuleFor(request => request.OrderCode)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(request => request.Phone)
            .NotEmpty()
            .MaximumLength(50);
    }
}
