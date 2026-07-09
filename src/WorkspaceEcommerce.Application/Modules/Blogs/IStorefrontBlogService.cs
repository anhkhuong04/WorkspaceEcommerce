using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public interface IStorefrontBlogService
{
    Task<Result<IReadOnlyCollection<StorefrontBlogPostDto>>> GetPublishedBlogPostsAsync(CancellationToken cancellationToken = default);

    Task<Result<StorefrontBlogPostDto>> GetBlogPostBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Result<BlogCommentDto>> SubmitCommentAsync(string slug, CreateCommentRequest request, CancellationToken cancellationToken = default);
}
