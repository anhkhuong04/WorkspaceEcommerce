using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Ordering;

public sealed class OrderItem : Entity
{
    public OrderItem(
        Guid id,
        Guid orderId,
        Guid productVariantId,
        string productNameSnapshot,
        string skuSnapshot,
        decimal unitPrice,
        int quantity,
        bool requiresInstallation)
        : base(id)
    {
        OrderId = RequiredId(orderId, "Order item order id cannot be empty.");
        ProductVariantId = RequiredId(productVariantId, "Order item product variant id cannot be empty.");
        ProductNameSnapshot = Guard.Required(productNameSnapshot, nameof(ProductNameSnapshot));
        SkuSnapshot = Guard.Required(skuSnapshot, nameof(SkuSnapshot));
        UnitPrice = Guard.NotNegative(unitPrice, nameof(UnitPrice));
        Quantity = RequiredPositiveQuantity(quantity);
        RequiresInstallation = requiresInstallation;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public string ProductNameSnapshot { get; private set; }

    public string SkuSnapshot { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public bool RequiresInstallation { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

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
            throw new DomainException("Order item quantity must be greater than zero.");
        }

        return quantity;
    }
}
