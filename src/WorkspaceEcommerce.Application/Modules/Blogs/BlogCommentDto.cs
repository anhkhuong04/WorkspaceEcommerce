using System;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed record BlogCommentDto(
    Guid Id,
    Guid BlogPostId,
    string AuthorName,
    string AuthorEmail,
    string Content,
    bool IsApproved,
    DateTimeOffset CreatedAt);
