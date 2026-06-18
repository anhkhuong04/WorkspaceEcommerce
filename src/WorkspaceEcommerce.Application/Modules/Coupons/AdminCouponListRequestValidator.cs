using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Coupons;

public sealed class AdminCouponListRequestValidator : AbstractValidator<AdminCouponListRequest>
{
    public AdminCouponListRequestValidator()
    {
        RuleFor(request => request.Search)
            .MaximumLength(250);
    }
}
