using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Catalog;

public sealed class Category : Entity
{
    public Category(
        Guid id,
        Guid? parentId,
        string name,
        string slug,
        int sortOrder,
        bool isActive = true)
        : base(id)
    {
        ParentId = parentId;
        Name = Guard.Required(name, nameof(Name));
        Slug = Guard.Required(slug, nameof(Slug));
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public Guid? ParentId { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public void UpdateDetails(string name, string slug, int sortOrder)
    {
        Name = Guard.Required(name, nameof(Name));
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
