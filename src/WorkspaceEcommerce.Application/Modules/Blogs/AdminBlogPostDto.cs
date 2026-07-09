using System;
using System.Collections.Generic;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed record AdminBlogPostDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string Content,
    string? ImageUrl,
    bool IsPublished,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<Guid> RelatedProductIds);
