namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record UpdateProductVariantRequest
{
    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Color { get; init; }

    public string? Size { get; init; }

    public decimal Price { get; init; }

    public decimal? CompareAtPrice { get; init; }

    public int StockQuantity { get; init; }

    public bool RequiresInstallation { get; init; }

    public bool IsActive { get; init; }
}
