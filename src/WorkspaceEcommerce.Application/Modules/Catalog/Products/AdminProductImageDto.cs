namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record AdminProductImageDto(
    Guid Id,
    Guid ProductId,
    string ImageUrl,
    string? AltText,
    int SortOrder);
