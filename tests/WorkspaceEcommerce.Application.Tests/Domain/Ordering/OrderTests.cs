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
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Null(order.PaidAt);
        Assert.Null(order.CustomerId);
        Assert.Equal(0m, order.ShippingFee);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(0m, order.TotalAmount);
    }

    [Theory]
    [InlineData(PaymentMethod.Cod, PaymentStatus.Unpaid)]
    [InlineData(PaymentMethod.ManualBankTransfer, PaymentStatus.Pending)]
    [InlineData(PaymentMethod.VNPay, PaymentStatus.Pending)]
    public void Constructor_SupportedPaymentMethod_SetsInitialPaymentStatus(
        PaymentMethod paymentMethod,
        PaymentStatus expectedPaymentStatus)
    {
        var order = CreateOrder(paymentMethod);

        Assert.Equal(paymentMethod, order.PaymentMethod);
        Assert.Equal(expectedPaymentStatus, order.PaymentStatus);
        Assert.Null(order.PaidAt);
    }

    [Fact]
    public void Constructor_UnsupportedPaymentMethod_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => CreateOrder((PaymentMethod)999));

        Assert.Equal("Order payment method is not supported.", exception.Message);
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
                PaymentMethod.Cod,
                "USD",
                1m));

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

    [Fact]
    public void MarkPaymentPending_WhenAlreadyPaid_ThrowsDomainException()
    {
        var order = CreateOrder(PaymentMethod.VNPay);
        order.MarkPaymentPaid(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainException>(order.MarkPaymentPending);

        Assert.Equal("Paid order payment cannot be moved back to pending.", exception.Message);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public void MarkPaymentPaid_ValidTimestamp_SetsPaidStatusAndPaidAt()
    {
        var paidAt = DateTimeOffset.UtcNow;
        var order = CreateOrder(PaymentMethod.VNPay);

        order.MarkPaymentPaid(paidAt);

        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(paidAt, order.PaidAt);
    }

    [Fact]
    public void MarkPaymentPaid_DefaultTimestamp_ThrowsDomainException()
    {
        var order = CreateOrder(PaymentMethod.VNPay);

        var exception = Assert.Throws<DomainException>(() => order.MarkPaymentPaid(default));

        Assert.Equal("Order paid timestamp is required.", exception.Message);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
    }

    [Fact]
    public void MarkPaymentFailed_WhenAlreadyPaid_ThrowsDomainException()
    {
        var order = CreateOrder(PaymentMethod.VNPay);
        order.MarkPaymentPaid(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainException>(order.MarkPaymentFailed);

        Assert.Equal("Paid order payment cannot be marked as failed.", exception.Message);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public void MarkPaymentCancelled_WhenAlreadyPaid_ThrowsDomainException()
    {
        var order = CreateOrder(PaymentMethod.VNPay);
        order.MarkPaymentPaid(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainException>(order.MarkPaymentCancelled);

        Assert.Equal("Paid order payment cannot be marked as cancelled.", exception.Message);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public void MarkPaymentFailed_FromPending_SetsFailedAndClearsPaidAt()
    {
        var order = CreateOrder(PaymentMethod.VNPay);

        order.MarkPaymentFailed();

        Assert.Equal(PaymentStatus.Failed, order.PaymentStatus);
        Assert.Null(order.PaidAt);
    }

    [Fact]
    public void MarkPaymentCancelled_FromPending_SetsCancelledAndClearsPaidAt()
    {
        var order = CreateOrder(PaymentMethod.VNPay);

        order.MarkPaymentCancelled();

        Assert.Equal(PaymentStatus.Cancelled, order.PaymentStatus);
        Assert.Null(order.PaidAt);
    }

    private static Order CreateOrder(PaymentMethod paymentMethod = PaymentMethod.Cod)
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
            paymentMethod,
            "USD",
            1m);
    }
}
