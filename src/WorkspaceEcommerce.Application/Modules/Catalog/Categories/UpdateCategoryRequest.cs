namespace WorkspaceEcommerce.Application.Modules.Catalog.Categories;

public sealed record UpdateCategoryRequest
{
    public Guid? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }
}
