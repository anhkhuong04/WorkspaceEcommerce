namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record AdminProductSpecificationDto(
    Guid Id,
    Guid ProductId,
    string Name,
    string Value,
    int SortOrder);
