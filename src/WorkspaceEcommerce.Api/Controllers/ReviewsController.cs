using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Reviews;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class ReviewsController(IReviewService reviewService) : ControllerBase
{
    [HttpGet("api/products/{slug}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ProductReviewSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProductReviews(
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.GetProductReviewsAsync(slug, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/products/{slug}/reviews")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateReview(
        string slug,
        [FromBody] CreateReviewBody body,
        CancellationToken cancellationToken)
    {
        var request = new CreateReviewRequest(slug, body.Rating, body.Comment);
        var result = await reviewService.CreateReviewAsync(request, cancellationToken);

        return this.ToApiResponse(result);
    }
}

public sealed record CreateReviewBody(int Rating, string? Comment);
