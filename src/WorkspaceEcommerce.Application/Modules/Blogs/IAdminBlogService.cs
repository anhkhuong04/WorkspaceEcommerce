using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public interface IAdminBlogService
{
    Task<Result<IReadOnlyCollection<AdminBlogPostDto>>> GetBlogPostsAsync(CancellationToken cancellationToken = default);

    Task<Result<AdminBlogPostDto>> GetBlogPostByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<AdminBlogPostDto>> CreateBlogPostAsync(CreateBlogPostRequest request, CancellationToken cancellationToken = default);

    Task<Result<AdminBlogPostDto>> UpdateBlogPostAsync(Guid id, UpdateBlogPostRequest request, CancellationToken cancellationToken = default);

    Task<Result<AdminBlogPostDto>> DeleteBlogPostAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<AdminBlogPostDto>> TogglePublishStatusAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<BlogCommentDto>>> GetBlogPostCommentsAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<Result<BlogCommentDto>> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default);
}
