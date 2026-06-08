using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Content.Banners;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize]
[Route("api/admin/banners")]
public sealed class BannersController(IAdminBannerService bannerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AdminBannerDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBanners(CancellationToken cancellationToken)
    {
        var result = await bannerService.GetBannersAsync(cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminBannerDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBanner(
        [FromBody] CreateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bannerService.CreateBannerAsync(request, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBannerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateBanner(
        Guid id,
        [FromBody] UpdateBannerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bannerService.UpdateBannerAsync(id, request, cancellationToken);

        return this.ToApiResponse(result);
    }
}
