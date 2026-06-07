using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Cart;

public sealed class CartItem : Entity
{
    public CartItem(
        Guid id,
        Guid cartId,
        Guid productVariantId,
        int quantity,
        decimal unitPriceSnapshot)
        : base(id)
    {
        CartId = RequiredId(cartId, "Cart item cart id cannot be empty.");
        ProductVariantId = RequiredId(productVariantId, "Cart item product variant id cannot be empty.");
        Quantity = RequiredPositiveQuantity(quantity);
        UnitPriceSnapshot = Guard.NotNegative(unitPriceSnapshot, nameof(UnitPriceSnapshot));
    }

    public Guid CartId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPriceSnapshot { get; private set; }

    public decimal LineTotal => UnitPriceSnapshot * Quantity;

    public void UpdateQuantity(int quantity)
    {
        Quantity = RequiredPositiveQuantity(quantity);
    }

    public void IncreaseQuantity(int quantity)
    {
        Quantity += RequiredPositiveQuantity(quantity);
    }

    private static Guid RequiredId(Guid value, string message)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException(message);
        }

        return value;
    }

    private static int RequiredPositiveQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Cart item quantity must be greater than zero.");
        }

        return quantity;
    }
}
