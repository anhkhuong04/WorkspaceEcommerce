using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

public interface IAdminBannerService
{
    Task<Result<IReadOnlyCollection<AdminBannerDto>>> GetBannersAsync(CancellationToken cancellationToken = default);

    Task<Result<AdminBannerDto>> CreateBannerAsync(
        CreateBannerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminBannerDto>> UpdateBannerAsync(
        Guid id,
        UpdateBannerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminBannerDto>> DeleteBannerAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
