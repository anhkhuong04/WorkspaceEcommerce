using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Blogs;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class BlogsController(IStorefrontBlogService blogService) : ControllerBase
{
    [HttpGet("api/blog-posts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<StorefrontBlogPostDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBlogPosts(CancellationToken cancellationToken)
    {
        var result = await blogService.GetPublishedBlogPostsAsync(cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/blog-posts/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<StorefrontBlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBlogPostBySlug(
        [FromRoute] string slug,
        CancellationToken cancellationToken)
    {
        var result = await blogService.GetBlogPostBySlugAsync(slug, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/blog-posts/{slug}/comments")]
    [ProducesResponseType(typeof(ApiResponse<BlogCommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitComment(
        [FromRoute] string slug,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await blogService.SubmitCommentAsync(slug, request, cancellationToken);

        return this.ToApiResponse(result);
    }
}
