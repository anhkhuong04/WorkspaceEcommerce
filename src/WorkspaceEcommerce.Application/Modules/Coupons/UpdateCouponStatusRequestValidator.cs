using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Coupons;

public sealed class UpdateCouponStatusRequestValidator : AbstractValidator<UpdateCouponStatusRequest>
{
    public UpdateCouponStatusRequestValidator()
    {
    }
}
