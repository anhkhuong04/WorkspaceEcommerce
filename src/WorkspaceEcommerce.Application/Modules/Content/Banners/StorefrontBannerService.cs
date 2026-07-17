using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Modules.Content;

namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

internal sealed class StorefrontBannerService(IAppDbContext dbContext) : IStorefrontBannerService
{
    public async Task<Result<IReadOnlyCollection<StorefrontBannerDto>>> GetActiveBannersAsync(
        CancellationToken cancellationToken = default)
    {
        var banners = await dbContext.Banners
            .Where(banner => banner.IsActive)
            .OrderBy(banner => banner.SortOrder)
            .ThenBy(banner => banner.Title)
            .Select(banner => new StorefrontBannerDto(
                banner.Id,
                banner.Title,
                banner.ImageUrl,
                banner.LinkUrl,
                banner.SortOrder))
            .ToArrayAsyncSafe(cancellationToken);

        return Result<IReadOnlyCollection<StorefrontBannerDto>>.Success(banners);
    }

    private static StorefrontBannerDto ToDto(Banner banner)
    {
        return new StorefrontBannerDto(
            banner.Id,
            banner.Title,
            banner.ImageUrl,
            banner.LinkUrl,
            banner.SortOrder);
    }
}
