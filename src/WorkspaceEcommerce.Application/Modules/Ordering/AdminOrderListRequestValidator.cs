using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class AdminOrderListRequestValidator : AbstractValidator<AdminOrderListRequest>
{
    public AdminOrderListRequestValidator()
    {
        RuleFor(request => request.PaymentStatus).IsInEnum().When(request => request.PaymentStatus.HasValue);
        RuleFor(request => request.Queue).Must(value => value is null or "" or "transfers" or "cod-remittance" or "delivery-failed" or "returns" or "refunds" or "payment-exceptions");
        RuleFor(request => request.Search)
            .MaximumLength(250);

        RuleFor(request => request.Status)
            .IsInEnum()
            .When(request => request.Status.HasValue);
    }
}
