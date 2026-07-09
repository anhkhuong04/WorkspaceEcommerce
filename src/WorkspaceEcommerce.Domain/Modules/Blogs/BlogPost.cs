using System;
using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Blogs;

public sealed class BlogPost : Entity
{
    public BlogPost(
        Guid id,
        string title,
        string slug,
        string summary,
        string content,
        string? imageUrl,
        bool isPublished = false)
        : base(id)
    {
        Title = Guard.Required(title, nameof(Title));
        Slug = Guard.Required(slug, nameof(Slug)).ToLowerInvariant();
        Summary = Guard.Required(summary, nameof(Summary));
        Content = Guard.Required(content, nameof(Content));
        ImageUrl = Guard.Optional(imageUrl);
        IsPublished = isPublished;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        if (isPublished)
        {
            PublishedAt = CreatedAt;
        }
    }

    public string Title { get; private set; }

    public string Slug { get; private set; }

    public string Summary { get; private set; }

    public string Content { get; private set; }

    public string? ImageUrl { get; private set; }

    public bool IsPublished { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateDetails(
        string title,
        string slug,
        string summary,
        string content,
        string? imageUrl)
    {
        Title = Guard.Required(title, nameof(Title));
        Slug = Guard.Required(slug, nameof(Slug)).ToLowerInvariant();
        Summary = Guard.Required(summary, nameof(Summary));
        Content = Guard.Required(content, nameof(Content));
        ImageUrl = Guard.Optional(imageUrl);
        Touch();
    }

    public void Publish()
    {
        if (!IsPublished)
        {
            IsPublished = true;
            PublishedAt = DateTimeOffset.UtcNow;
            Touch();
        }
    }

    public void Unpublish()
    {
        if (IsPublished)
        {
            IsPublished = false;
            PublishedAt = null;
            Touch();
        }
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
