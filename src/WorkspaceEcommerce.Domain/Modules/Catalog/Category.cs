using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Catalog;

public sealed class Category : Entity
{
    public Category(
        Guid id,
        Guid? parentId,
        LocalizedText name,
        string slug,
        int sortOrder,
        bool isActive = true)
        : base(id)
    {
        ParentId = parentId;
        Name = name ?? new LocalizedText();
        Slug = Guard.Required(slug, nameof(Slug));
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public Guid? ParentId { get; private set; }

    public LocalizedText Name { get; private set; }

    public string Slug { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public void UpdateDetails(LocalizedText name, string slug, int sortOrder)
    {
        Name = name ?? new LocalizedText();
        Slug = Guard.Required(slug, nameof(Slug));
        SortOrder = sortOrder;
    }

    public void MoveToParent(Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new DomainException("Category cannot be its own parent.");
        }

        ParentId = parentId;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
