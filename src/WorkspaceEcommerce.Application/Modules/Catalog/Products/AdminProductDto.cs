namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record AdminProductDto(
    Guid Id,
    Guid CategoryId,
    string? CategoryName,
    string Name,
    string Slug,
    string? Description,
    bool IsFeatured,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<AdminProductVariantDto> Variants,
    IReadOnlyCollection<AdminProductImageDto> Images,
    IReadOnlyCollection<AdminProductSpecificationDto> Specifications);
