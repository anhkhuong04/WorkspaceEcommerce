using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

public sealed class CustomerOrderListRequestValidator : AbstractValidator<CustomerOrderListRequest>
{
    public CustomerOrderListRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum()
            .When(request => request.Status.HasValue);
    }
}
