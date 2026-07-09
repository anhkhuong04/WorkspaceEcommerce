namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record CreateProductRequest
{
    public Guid CategoryId { get; init; }

    public IDictionary<string, string> Name { get; init; } = new Dictionary<string, string>();

    public string Slug { get; init; } = string.Empty;

    public IDictionary<string, string>? Description { get; init; }

    public bool IsFeatured { get; init; }

    public bool IsActive { get; init; } = true;
}
