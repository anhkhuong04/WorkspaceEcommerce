namespace WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

public sealed record StorefrontProductDetailDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Slug,
    string? Description,
    bool IsFeatured,
    IReadOnlyCollection<StorefrontProductVariantDto> Variants,
    IReadOnlyCollection<StorefrontProductImageDto> Images,
    IReadOnlyCollection<StorefrontProductSpecificationDto> Specifications);
