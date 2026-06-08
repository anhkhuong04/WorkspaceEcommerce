using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class AdminOrderListRequestValidator : AbstractValidator<AdminOrderListRequest>
{
    public AdminOrderListRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum()
            .When(request => request.Status.HasValue);
    }
}
