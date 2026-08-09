using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Blogs;

public sealed class BlogComment : Entity
{
    private BlogComment()
    {
    }

    public BlogComment(
        Guid id,
        Guid blogPostId,
        string authorName,
        string authorEmail,
        string content)
        : base(id)
    {
        BlogPostId = blogPostId;
        AuthorName = Guard.Required(authorName, nameof(AuthorName));
        AuthorEmail = Guard.Required(authorEmail, nameof(AuthorEmail));
        Content = Guard.Required(content, nameof(Content));
        ModerationStatus = BlogCommentModerationStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid BlogPostId { get; private set; }

    public string AuthorName { get; private set; } = default!;

    public string AuthorEmail { get; private set; } = default!;

    public string Content { get; private set; } = default!;

    public BlogCommentModerationStatus ModerationStatus { get; private set; }

    public DateTimeOffset? ModeratedAt { get; private set; }

    public string? ModeratedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsPublic => ModerationStatus == BlogCommentModerationStatus.Approved;

    public void Approve(string moderatorIdentity)
    {
        SetModeration(BlogCommentModerationStatus.Approved, moderatorIdentity);
    }

    public void Reject(string moderatorIdentity)
    {
        SetModeration(BlogCommentModerationStatus.Rejected, moderatorIdentity);
    }

    private void SetModeration(BlogCommentModerationStatus status, string moderatorIdentity)
    {
        ModerationStatus = status;
        ModeratedBy = Guard.Required(moderatorIdentity, nameof(moderatorIdentity));
        ModeratedAt = DateTimeOffset.UtcNow;
    }
}

public enum BlogCommentModerationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
