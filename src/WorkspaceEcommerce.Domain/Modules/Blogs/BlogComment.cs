using System;
using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Blogs;

public sealed class BlogComment : Entity
{
    public BlogComment(
        Guid id,
        Guid blogPostId,
        string authorName,
        string authorEmail,
        string content,
        bool isApproved = true)
        : base(id)
    {
        BlogPostId = blogPostId;
        AuthorName = Guard.Required(authorName, nameof(AuthorName));
        AuthorEmail = Guard.Required(authorEmail, nameof(AuthorEmail));
        Content = Guard.Required(content, nameof(Content));
        IsApproved = isApproved;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid BlogPostId { get; private set; }

    public string AuthorName { get; private set; }

    public string AuthorEmail { get; private set; }

    public string Content { get; private set; }

    public bool IsApproved { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Approve()
    {
        IsApproved = true;
    }

    public void Unapprove()
    {
        IsApproved = false;
    }
}
