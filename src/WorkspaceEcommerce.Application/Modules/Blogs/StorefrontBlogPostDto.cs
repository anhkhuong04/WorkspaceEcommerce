using System;
using System.Collections.Generic;
using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed record StorefrontBlogPostDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string Content,
    string? ImageUrl,
    DateTimeOffset? PublishedAt,
    IReadOnlyCollection<StorefrontProductListItemDto> RelatedProducts,
    IReadOnlyCollection<PublicBlogCommentDto> Comments);

public sealed record PublicBlogCommentDto(Guid Id, string AuthorName, string Content, DateTimeOffset CreatedAt);
