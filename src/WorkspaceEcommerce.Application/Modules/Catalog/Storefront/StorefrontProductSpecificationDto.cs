namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

public sealed record StorefrontProductSpecificationDto(
    Guid Id,
    string Name,
    string Value,
    int SortOrder);
