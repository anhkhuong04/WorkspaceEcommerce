using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Cart;

public sealed class Cart : Entity
{
    private readonly List<CartItem> _items = [];

    public Cart(Guid id, Guid? customerId, string? sessionId)
        : base(id)
    {
        CustomerId = NormalizeCustomerId(customerId);
        SessionId = Guard.Optional(sessionId);

        if (CustomerId is null && SessionId is null)
        {
            throw new DomainException("Cart must belong to a customer or session.");
        }

        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid? CustomerId { get; private set; }

    public string? SessionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items;

    public int TotalQuantity => _items.Sum(item => item.Quantity);

    public decimal TotalAmount => _items.Sum(item => item.LineTotal);

    public CartItem AddItem(Guid id, Guid productVariantId, int quantity, decimal unitPriceSnapshot)
    {
        var existingItem = _items.FirstOrDefault(item => item.ProductVariantId == productVariantId);
        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
            Touch();

            return existingItem;
        }

        var item = new CartItem(id, Id, productVariantId, quantity, unitPriceSnapshot);
        _items.Add(item);
        Touch();

        return item;
    }

    public CartItem UpdateItemQuantity(Guid itemId, int quantity)
    {
        var item = FindItem(itemId);
        item.UpdateQuantity(quantity);
        Touch();

        return item;
    }

    public CartItem RemoveItem(Guid itemId)
    {
        var item = FindItem(itemId);
        _items.Remove(item);
        Touch();

        return item;
    }

    private CartItem FindItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(existing => existing.Id == itemId);

        return item ?? throw new DomainException("Cart item was not found.");
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static Guid? NormalizeCustomerId(Guid? customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Cart customer id cannot be empty.");
        }

        return customerId;
    }
}
