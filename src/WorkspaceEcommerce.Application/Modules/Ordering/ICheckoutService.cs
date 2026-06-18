using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public interface ICheckoutService
{
    Task<Result<CheckoutCouponValidationResponse>> ValidateCouponAsync(
        ValidateCheckoutCouponRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CheckoutResponse>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default);
}
