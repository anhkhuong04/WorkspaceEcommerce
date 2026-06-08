using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Content;

public sealed class Banner : Entity
{
    public Banner(
        Guid id,
        string title,
        string imageUrl,
        string? linkUrl,
        int sortOrder,
        bool isActive = true)
        : base(id)
    {
        Title = Guard.Required(title, nameof(Title));
        ImageUrl = Guard.Required(imageUrl, nameof(ImageUrl));
        LinkUrl = Guard.Optional(linkUrl);
        SortOrder = sortOrder;
        IsActive = isActive;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Title { get; private set; }

    public string ImageUrl { get; private set; }

    public string? LinkUrl { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateDetails(string title, string imageUrl, string? linkUrl, int sortOrder)
    {
        Title = Guard.Required(title, nameof(Title));
        ImageUrl = Guard.Required(imageUrl, nameof(ImageUrl));
        LinkUrl = Guard.Optional(linkUrl);
        SortOrder = sortOrder;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
