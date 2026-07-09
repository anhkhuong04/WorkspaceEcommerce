namespace WorkspaceEcommerce.Application.Modules.Catalog.Categories;

public sealed record CreateCategoryRequest
{
    public Guid? ParentId { get; init; }

    public IDictionary<string, string> Name { get; init; } = new Dictionary<string, string>();

    public string Slug { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; } = true;
}
