using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Catalog;

public sealed class Product : Entity
{
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];
    private readonly List<ProductSpecification> _specifications = [];

    public Product(
        Guid id,
        Guid categoryId,
        LocalizedText name,
        string slug,
        LocalizedText? description,
        bool isFeatured = false,
        bool isActive = true)
        : base(id)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("Product category id cannot be empty.");
        }

        CategoryId = categoryId;
        Name = name ?? new LocalizedText();
        Slug = Guard.Required(slug, nameof(Slug));
        Description = description;
        IsFeatured = isFeatured;
        IsActive = isActive;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CategoryId { get; private set; }

    public LocalizedText Name { get; private set; }

    public string Slug { get; private set; }

    public LocalizedText? Description { get; private set; }

    public bool IsFeatured { get; private set; }

    public bool IsActive { get; private set; }

    public double AverageRating { get; private set; }

    public int ReviewCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ProductVariant> Variants => _variants;

    public IReadOnlyCollection<ProductImage> Images => _images;

    public IReadOnlyCollection<ProductSpecification> Specifications => _specifications;

    public void UpdateDetails(Guid categoryId, LocalizedText name, string slug, LocalizedText? description)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("Product category id cannot be empty.");
        }

        CategoryId = categoryId;
        Name = name ?? new LocalizedText();
        Slug = Guard.Required(slug, nameof(Slug));
        Description = description;
        Touch();
    }

    public void MarkAsFeatured()
    {
        IsFeatured = true;
        Touch();
    }

    public void UnmarkAsFeatured()
    {
        IsFeatured = false;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public ProductVariant AddVariant(
        Guid id,
        string sku,
        string name,
        string? color,
        string? size,
        decimal price,
        decimal? compareAtPrice,
        int stockQuantity,
        bool requiresInstallation,
        bool isActive = true)
    {
        if (_variants.Any(variant => string.Equals(variant.Sku, sku, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("Product variant SKU must be unique within a product.");
        }

        var variant = new ProductVariant(
            id,
            Id,
            sku,
            name,
            color,
            size,
            price,
            compareAtPrice,
            stockQuantity,
            requiresInstallation,
            isActive);

        _variants.Add(variant);
        Touch();

        return variant;
    }

    public ProductImage AddImage(Guid id, string imageUrl, string? altText, int sortOrder)
    {
        var image = new ProductImage(id, Id, imageUrl, altText, sortOrder);

        _images.Add(image);
        Touch();

        return image;
    }

    public ProductSpecification AddSpecification(Guid id, string name, string value, int sortOrder)
    {
        var specification = new ProductSpecification(id, Id, name, value, sortOrder);

        _specifications.Add(specification);
        Touch();

        return specification;
    }

    public void UpdateRatingStats(double averageRating, int reviewCount)
    {
        if (reviewCount < 0)
        {
            throw new DomainException("Review count cannot be negative.");
        }

        AverageRating = reviewCount > 0 ? Math.Round(averageRating, 2) : 0;
        ReviewCount = reviewCount;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
