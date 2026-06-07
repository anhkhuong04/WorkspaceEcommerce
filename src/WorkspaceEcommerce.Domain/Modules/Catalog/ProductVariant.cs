using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Catalog;

public sealed class ProductVariant : Entity
{
    public ProductVariant(
        Guid id,
        Guid productId,
        string sku,
        string name,
        string? color,
        string? size,
        decimal price,
        decimal? compareAtPrice,
        int stockQuantity,
        bool requiresInstallation,
        bool isActive = true)
        : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Product variant product id cannot be empty.");
        }

        ProductId = productId;
        Sku = Guard.Required(sku, nameof(Sku));
        Name = Guard.Required(name, nameof(Name));
        Color = Guard.Optional(color);
        Size = Guard.Optional(size);
        RequiresInstallation = requiresInstallation;
        IsActive = isActive;

        UpdatePricing(price, compareAtPrice);
        UpdateStock(stockQuantity);
    }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public string? Color { get; private set; }

    public string? Size { get; private set; }

    public decimal Price { get; private set; }

    public decimal? CompareAtPrice { get; private set; }

    public int StockQuantity { get; private set; }

    public bool RequiresInstallation { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(
        string sku,
        string name,
        string? color,
        string? size,
        bool requiresInstallation)
    {
        Sku = Guard.Required(sku, nameof(Sku));
        Name = Guard.Required(name, nameof(Name));
        Color = Guard.Optional(color);
        Size = Guard.Optional(size);
        RequiresInstallation = requiresInstallation;
    }

    public void UpdatePricing(decimal price, decimal? compareAtPrice)
    {
        Price = Guard.NotNegative(price, nameof(Price));

        if (compareAtPrice is not null && compareAtPrice.Value < Price)
        {
            throw new DomainException("Compare-at price cannot be lower than price.");
        }

        CompareAtPrice = compareAtPrice;
    }

    public void UpdateStock(int stockQuantity)
    {
        StockQuantity = Guard.NotNegative(stockQuantity, nameof(StockQuantity));
    }

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Stock decrease quantity must be greater than zero.");
        }

        if (quantity > StockQuantity)
        {
            throw new DomainException("Insufficient stock quantity.");
        }

        StockQuantity -= quantity;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
