using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

public interface IStorefrontBannerService
{
    Task<Result<IReadOnlyCollection<StorefrontBannerDto>>> GetActiveBannersAsync(
        CancellationToken cancellationToken = default);
}
