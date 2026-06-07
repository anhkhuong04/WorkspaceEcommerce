namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record AdminProductVariantDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    string Name,
    string? Color,
    string? Size,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    bool RequiresInstallation,
    bool IsActive);
