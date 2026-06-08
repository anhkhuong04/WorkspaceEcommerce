using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Content.Banners;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class BannersController(IStorefrontBannerService bannerService) : ControllerBase
{
    [HttpGet("api/banners")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<StorefrontBannerDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBanners(CancellationToken cancellationToken)
    {
        var result = await bannerService.GetActiveBannersAsync(cancellationToken);

        return this.ToApiResponse(result);
    }
}
