using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Cart;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Application.Tests.Domain.Cart;

public sealed class CartTests
{
    [Fact]
    public void Constructor_NoCustomerOrSession_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => new CartAggregate(Guid.NewGuid(), null, null));

        Assert.Equal("Cart must belong to a customer or session.", exception.Message);
    }

    [Fact]
    public void AddItem_NewVariant_AddsItemAndCalculatesTotals()
    {
        var cart = CreateCart();
        var variantId = Guid.NewGuid();

        var item = cart.AddItem(Guid.NewGuid(), variantId, 2, 100m);

        Assert.Equal(variantId, item.ProductVariantId);
        Assert.Equal(2, cart.TotalQuantity);
        Assert.Equal(200m, cart.TotalAmount);
    }

    [Fact]
    public void AddItem_SameVariant_IncreasesQuantityAndKeepsOriginalPriceSnapshot()
    {
        var cart = CreateCart();
        var variantId = Guid.NewGuid();

        cart.AddItem(Guid.NewGuid(), variantId, 1, 100m);
        var item = cart.AddItem(Guid.NewGuid(), variantId, 2, 120m);

        Assert.Single(cart.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(100m, item.UnitPriceSnapshot);
        Assert.Equal(300m, cart.TotalAmount);
    }

    [Fact]
    public void UpdateItemQuantity_ExistingItem_UpdatesTotals()
    {
        var cart = CreateCart();
        var item = cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1, 100m);

        cart.UpdateItemQuantity(item.Id, 3);

        Assert.Equal(3, item.Quantity);
        Assert.Equal(300m, cart.TotalAmount);
    }

    [Fact]
    public void RemoveItem_ExistingItem_RemovesFromCart()
    {
        var cart = CreateCart();
        var item = cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1, 100m);

        var removedItem = cart.RemoveItem(item.Id);

        Assert.Equal(item.Id, removedItem.Id);
        Assert.Empty(cart.Items);
        Assert.Equal(0m, cart.TotalAmount);
    }

    [Fact]
    public void CartItem_InvalidQuantity_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new CartItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, 100m));

        Assert.Equal("Cart item quantity must be greater than zero.", exception.Message);
    }

    private static CartAggregate CreateCart()
    {
        return new CartAggregate(Guid.NewGuid(), null, "session-1");
    }
}
