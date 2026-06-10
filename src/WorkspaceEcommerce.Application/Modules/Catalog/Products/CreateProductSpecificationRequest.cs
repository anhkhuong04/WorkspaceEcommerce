namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record CreateProductSpecificationRequest
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}
