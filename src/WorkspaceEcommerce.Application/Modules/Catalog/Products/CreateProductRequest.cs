namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record CreateProductRequest
{
    public Guid CategoryId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsFeatured { get; init; }

    public bool IsActive { get; init; } = true;
}
