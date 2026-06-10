namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record CreateProductImageRequest
{
    public string ImageUrl { get; init; } = string.Empty;

    public string? AltText { get; init; }

    public int SortOrder { get; init; }
}
