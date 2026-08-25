using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Media;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
public sealed class MediaController(IMediaStorageService mediaStorageService) : ControllerBase
{
    [HttpPost("api/admin/media")]
    [RequestSizeLimit(2 * 1024 * 1024 + 512 * 1024)] // 2.5 MB — image cap (2 MB) + multipart overhead
    [ProducesResponseType(typeof(ApiResponse<MediaUploadResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? folder,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return this.ToApiResponse(
                WorkspaceEcommerce.Application.Common.Models.Result<MediaUploadResult>.Validation(["Image file is required."]),
                StatusCodes.Status201Created);
        }

        await using var stream = file.OpenReadStream();
        var result = await mediaStorageService.SaveAsync(
            new MediaUploadRequest(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                folder,
                User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name),
            cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }
}
