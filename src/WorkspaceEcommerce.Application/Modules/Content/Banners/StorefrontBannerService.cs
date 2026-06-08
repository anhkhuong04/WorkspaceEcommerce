using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Content;

namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

internal sealed class StorefrontBannerService(IAppDbContext dbContext) : IStorefrontBannerService
{
    public Task<Result<IReadOnlyCollection<StorefrontBannerDto>>> GetActiveBannersAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var banners = dbContext.Banners
            .Where(banner => banner.IsActive)
            .OrderBy(banner => banner.SortOrder)
            .ThenBy(banner => banner.Title)
            .Select(ToDto)
            .ToArray();

        return Task.FromResult(Result<IReadOnlyCollection<StorefrontBannerDto>>.Success(banners));
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
