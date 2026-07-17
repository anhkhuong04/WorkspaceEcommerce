using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal static class CheckoutResultMapper
{
    public static Result<CheckoutResponse> ToCheckoutFailure<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CheckoutResponse>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CheckoutResponse>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CheckoutResponse>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CheckoutResponse>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CheckoutResponse>.Failure(result.Errors)
        };
    }

    public static Result<CheckoutCouponValidationResponse> ToCouponValidationFailure<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CheckoutCouponValidationResponse>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CheckoutCouponValidationResponse>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CheckoutCouponValidationResponse>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CheckoutCouponValidationResponse>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CheckoutCouponValidationResponse>.Failure(result.Errors)
        };
    }
}
