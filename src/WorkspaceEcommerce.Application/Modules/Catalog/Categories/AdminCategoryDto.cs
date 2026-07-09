namespace WorkspaceEcommerce.Application.Modules.Catalog.Categories;

public sealed record AdminCategoryDto(
    Guid Id,
    Guid? ParentId,
    IDictionary<string, string> Name,
    string Slug,
    bool IsActive,
    int SortOrder,
    IReadOnlyCollection<AdminCategoryDto> Children);
