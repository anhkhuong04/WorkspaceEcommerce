using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Reviews;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public sealed class ReviewsController(IAdminReviewService reviewService) : ControllerBase
{
    [HttpGet("api/admin/reviews")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminReviewListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReviews(
        [FromQuery] AdminReviewListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.GetReviewsAsync(request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpDelete("api/admin/reviews/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteReview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.DeleteReviewAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }
}
