using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Coupons;

public interface IAdminCouponService
{
    Task<Result<PagedResult<AdminCouponDto>>> GetCouponsAsync(
        AdminCouponListRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminCouponDto>> GetCouponByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<AdminCouponDto>> CreateCouponAsync(
        CreateCouponRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminCouponDto>> UpdateCouponAsync(
        Guid id,
        UpdateCouponRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminCouponDto>> UpdateStatusAsync(
        Guid id,
        UpdateCouponStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminCouponDto>> DeleteCouponAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
