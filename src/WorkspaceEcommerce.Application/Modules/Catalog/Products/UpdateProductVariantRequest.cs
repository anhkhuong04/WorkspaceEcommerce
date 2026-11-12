namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record UpdateProductVariantRequest
{

    public decimal? WeightKg { get; init; }
    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
    public bool UpdateDimensions { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Color { get; init; }

    public string? Size { get; init; }

    public decimal Price { get; init; }

    public decimal? CompareAtPrice { get; init; }

    public int StockQuantity { get; init; }

    public int? ExpectedStockQuantity { get; init; }
    public string? StockChangeReason { get; init; }

    public bool RequiresInstallation { get; init; }

    public bool IsActive { get; init; }
}
