using System;
using System.Collections.Generic;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed record UpdateBlogPostRequest(
    string Title,
    string Slug,
    string Summary,
    string Content,
    string? ImageUrl,
    bool IsPublished,
    IReadOnlyCollection<Guid> RelatedProductIds);
