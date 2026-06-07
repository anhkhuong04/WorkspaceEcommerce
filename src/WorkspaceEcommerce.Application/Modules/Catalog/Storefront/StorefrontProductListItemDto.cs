namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

public sealed record StorefrontProductListItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Slug,
    string? Description,
    bool IsFeatured,
    decimal? MinPrice,
    decimal? CompareAtPrice,
    bool IsInStock,
    string? ImageUrl);
