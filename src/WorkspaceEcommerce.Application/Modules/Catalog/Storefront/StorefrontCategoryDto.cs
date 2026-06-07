namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

public sealed record StorefrontCategoryDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    int SortOrder,
    IReadOnlyCollection<StorefrontCategoryDto> Children);
