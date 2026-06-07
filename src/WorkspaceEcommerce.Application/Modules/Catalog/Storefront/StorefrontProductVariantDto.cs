namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

public sealed record StorefrontProductVariantDto(
    Guid Id,
    string Sku,
    string Name,
    string? Color,
    string? Size,
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    bool RequiresInstallation);
