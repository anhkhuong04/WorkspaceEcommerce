using System;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed record BlogCommentDto(
    Guid Id,
    Guid BlogPostId,
    string AuthorName,
    string AuthorEmail,
    string Content,
    BlogCommentModerationStatus ModerationStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModeratedAt,
    string? ModeratedBy);

public sealed record CommentSubmissionAcknowledgement(string Message);
