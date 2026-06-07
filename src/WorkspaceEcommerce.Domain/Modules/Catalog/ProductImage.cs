using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Catalog;

public sealed class ProductImage : Entity
{
    public ProductImage(
        Guid id,
        Guid productId,
        string imageUrl,
        string? altText,
        int sortOrder)
        : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Product image product id cannot be empty.");
        }

        ProductId = productId;
        ImageUrl = Guard.Required(imageUrl, nameof(ImageUrl));
        AltText = Guard.Optional(altText);
        SortOrder = sortOrder;
    }

    public Guid ProductId { get; private set; }

    public string ImageUrl { get; private set; }

    public string? AltText { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(string imageUrl, string? altText, int sortOrder)
    {
        ImageUrl = Guard.Required(imageUrl, nameof(ImageUrl));
        AltText = Guard.Optional(altText);
        SortOrder = sortOrder;
    }
}
