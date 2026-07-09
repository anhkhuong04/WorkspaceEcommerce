using System;
using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Blogs;

public sealed class BlogPostRelatedProduct : Entity
{
    public BlogPostRelatedProduct(
        Guid id,
        Guid blogPostId,
        Guid productId)
        : base(id)
    {
        BlogPostId = blogPostId;
        ProductId = productId;
    }

    public Guid BlogPostId { get; private set; }

    public Guid ProductId { get; private set; }
}
