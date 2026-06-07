namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

public sealed record StorefrontProductImageDto(
    Guid Id,
    string ImageUrl,
    string? AltText,
    int SortOrder);
