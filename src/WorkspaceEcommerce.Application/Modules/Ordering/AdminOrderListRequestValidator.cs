using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class AdminOrderListRequestValidator : AbstractValidator<AdminOrderListRequest>
{
    public AdminOrderListRequestValidator()
    {
        RuleFor(request => request.Search)
            .MaximumLength(250);

        RuleFor(request => request.Status)
            .IsInEnum()
            .When(request => request.Status.HasValue);
    }
}
