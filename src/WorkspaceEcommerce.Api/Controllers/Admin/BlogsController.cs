using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Blogs;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public sealed class BlogsController(IAdminBlogService blogService) : ControllerBase
{
    [HttpGet("api/admin/blog-posts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AdminBlogPostDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBlogPosts(CancellationToken cancellationToken)
    {
        var result = await blogService.GetBlogPostsAsync(cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/admin/blog-posts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBlogPostById(Guid id, CancellationToken cancellationToken)
    {
        var result = await blogService.GetBlogPostByIdAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/blog-posts")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBlogPost(
        [FromBody] CreateBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var result = await blogService.CreateBlogPostAsync(request, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPut("api/admin/blog-posts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateBlogPost(
        Guid id,
        [FromBody] UpdateBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var result = await blogService.UpdateBlogPostAsync(id, request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpDelete("api/admin/blog-posts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteBlogPost(Guid id, CancellationToken cancellationToken)
    {
        var result = await blogService.DeleteBlogPostAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/blog-posts/{id:guid}/toggle-publish")]
    [ProducesResponseType(typeof(ApiResponse<AdminBlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TogglePublish(Guid id, CancellationToken cancellationToken)
    {
        var result = await blogService.TogglePublishStatusAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/admin/blog-posts/{id:guid}/comments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<BlogCommentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBlogPostComments(Guid id, CancellationToken cancellationToken)
    {
        var result = await blogService.GetBlogPostCommentsAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpDelete("api/admin/blog-comments/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BlogCommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteComment(Guid id, CancellationToken cancellationToken)
    {
        var result = await blogService.DeleteCommentAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }
}
