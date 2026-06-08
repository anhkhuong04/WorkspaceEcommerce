using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Domain.Ordering;

public sealed class OrderTests
{
    [Fact]
    public void Constructor_ValidGuestCheckout_CreatesPendingOrderWithZeroFees()
    {
        var order = CreateOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(PaymentMethod.Cod, order.PaymentMethod);
        Assert.Null(order.CustomerId);
        Assert.Equal(0m, order.ShippingFee);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Fact]
    public void Constructor_MissingRecipientInfo_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Order(
                Guid.NewGuid(),
                "ORD-001",
                null,
                string.Empty,
                "0900000000",
                null,
                "Shipping address",
                null,
                PaymentMethod.Cod));

        Assert.Equal("CustomerName is required.", exception.Message);
    }

    [Fact]
    public void AddItem_ValidSnapshot_AddsItemAndCalculatesTotals()
    {
        var order = CreateOrder();

        var item = order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Standing Desk",
            "DESK-001",
            100m,
            2,
            requiresInstallation: true);

        Assert.Equal("Standing Desk", item.ProductNameSnapshot);
        Assert.Equal("DESK-001", item.SkuSnapshot);
        Assert.True(item.RequiresInstallation);
        Assert.Equal(200m, order.Subtotal);
        Assert.Equal(200m, order.TotalAmount);
        Assert.Single(order.Items);
    }

    [Fact]
    public void AddItem_DuplicateVariant_ThrowsDomainException()
    {
        var order = CreateOrder();
        var variantId = Guid.NewGuid();
        order.AddItem(Guid.NewGuid(), variantId, "Standing Desk", "DESK-001", 100m, 1, false);

        var exception = Assert.Throws<DomainException>(() =>
            order.AddItem(Guid.NewGuid(), variantId, "Standing Desk", "DESK-001", 100m, 1, false));

        Assert.Equal("Order item product variant must be unique within an order.", exception.Message);
    }

    [Fact]
    public void AddItem_InvalidQuantity_ThrowsDomainException()
    {
        var order = CreateOrder();

        var exception = Assert.Throws<DomainException>(() =>
            order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 100m, 0, false));

        Assert.Equal("Order item quantity must be greater than zero.", exception.Message);
    }

    [Fact]
    public void RecordCreated_FirstCall_AddsInitialStatusHistory()
    {
        var order = CreateOrder();

        var history = order.RecordCreated(Guid.NewGuid(), "Created by checkout", changedBy: null);

        Assert.Null(history.FromStatus);
        Assert.Equal(OrderStatus.Pending, history.ToStatus);
        Assert.Single(order.StatusHistory);
    }

    [Fact]
    public void ChangeStatus_ValidTransition_UpdatesStatusAndAddsHistory()
    {
        var order = CreateOrder();

        var history = order.ChangeStatus(
            Guid.NewGuid(),
            OrderStatus.Confirmed,
            "Confirmed by admin",
            "admin@example.com");

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(OrderStatus.Pending, history.FromStatus);
        Assert.Equal(OrderStatus.Confirmed, history.ToStatus);
        Assert.Equal("admin@example.com", history.ChangedBy);
        Assert.Single(order.StatusHistory);
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_ThrowsDomainException()
    {
        var order = CreateOrder();

        var exception = Assert.Throws<DomainException>(() =>
            order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, null, null));

        Assert.Equal("Order status cannot change from Pending to Completed.", exception.Message);
    }

    [Fact]
    public void ChangeStatus_SameStatus_ThrowsDomainException()
    {
        var order = CreateOrder();

        var exception = Assert.Throws<DomainException>(() =>
            order.ChangeStatus(Guid.NewGuid(), OrderStatus.Pending, null, null));

        Assert.Equal("Order status is already set.", exception.Message);
    }

    [Fact]
    public void ChangeStatus_CompletedIsTerminal_ThrowsDomainException()
    {
        var order = CreateOrder();
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, null, null);

        var exception = Assert.Throws<DomainException>(() =>
            order.ChangeStatus(Guid.NewGuid(), OrderStatus.Cancelled, null, null));

        Assert.Equal("Order status cannot change from Completed to Cancelled.", exception.Message);
    }

    [Fact]
    public void ChangeStatus_FailedDeliveryCanReturnToShippingOrCancel()
    {
        var order = CreateOrder();
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.FailedDelivery, null, null);

        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, null);

        Assert.Equal(OrderStatus.Shipping, order.Status);
    }

    private static Order CreateOrder()
    {
        return new Order(
            Guid.NewGuid(),
            "ORD-001",
            null,
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "123 Shipping Street",
            "Call before delivery",
            PaymentMethod.Cod);
    }
}
