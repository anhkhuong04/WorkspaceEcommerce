using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Ordering;

public sealed class Order : Entity
{
    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusHistory> _statusHistory = [];

    public Order(
        Guid id,
        string orderCode,
        Guid? customerId,
        string customerName,
        string customerPhone,
        string? customerEmail,
        string shippingAddress,
        string? note,
        PaymentMethod paymentMethod)
        : base(id)
    {
        OrderCode = Guard.Required(orderCode, nameof(OrderCode));
        CustomerId = NormalizeCustomerId(customerId);
        CustomerName = Guard.Required(customerName, nameof(CustomerName));
        CustomerPhone = Guard.Required(customerPhone, nameof(CustomerPhone));
        CustomerEmail = Guard.Optional(customerEmail);
        ShippingAddress = Guard.Required(shippingAddress, nameof(ShippingAddress));
        Note = Guard.Optional(note);
        PaymentMethod = paymentMethod;
        Status = OrderStatus.Pending;
        ShippingFee = 0m;
        DiscountAmount = 0m;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string OrderCode { get; private set; }

    public Guid? CustomerId { get; private set; }

    public string CustomerName { get; private set; }

    public string CustomerPhone { get; private set; }

    public string? CustomerEmail { get; private set; }

    public string ShippingAddress { get; private set; }

    public string? Note { get; private set; }

    public Guid? CouponId { get; private set; }

    public string? CouponCodeSnapshot { get; private set; }

    public string? CouponNameSnapshot { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal ShippingFee { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public OrderStatus Status { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? TrackingCode { get; private set; }

    public Guid? ShipmentId { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory;

    public OrderItem AddItem(
        Guid id,
        Guid productVariantId,
        string productNameSnapshot,
        string skuSnapshot,
        decimal unitPrice,
        int quantity,
        bool requiresInstallation)
    {
        if (_items.Any(item => item.ProductVariantId == productVariantId))
        {
            throw new DomainException("Order item product variant must be unique within an order.");
        }

        var item = new OrderItem(
            id,
            Id,
            productVariantId,
            productNameSnapshot,
            skuSnapshot,
            unitPrice,
            quantity,
            requiresInstallation);

        _items.Add(item);
        RecalculateTotals();
        Touch();

        return item;
    }

    public void ApplyCoupon(
        Guid couponId,
        string couponCodeSnapshot,
        string couponNameSnapshot,
        decimal discountAmount)
    {
        if (couponId == Guid.Empty)
        {
            throw new DomainException("Order coupon id cannot be empty.");
        }

        if (CouponId is not null)
        {
            throw new DomainException("Order already has a coupon applied.");
        }

        if (discountAmount <= 0m)
        {
            throw new DomainException("Order discount amount must be greater than zero.");
        }

        var maximumDiscount = Subtotal + ShippingFee;
        if (discountAmount > maximumDiscount)
        {
            throw new DomainException("Order discount amount cannot exceed order subtotal and shipping fee.");
        }

        CouponId = couponId;
        CouponCodeSnapshot = Guard.Required(couponCodeSnapshot, nameof(CouponCodeSnapshot)).ToUpperInvariant();
        CouponNameSnapshot = Guard.Required(couponNameSnapshot, nameof(CouponNameSnapshot));
        DiscountAmount = discountAmount;
        RecalculateTotals();
        Touch();
    }

    public void SetShippingFee(decimal shippingFee)
    {
        ShippingFee = Guard.NotNegative(shippingFee, nameof(ShippingFee));
        RecalculateTotals();
        Touch();
    }

    public void UpdateShipmentInfo(string trackingCode, Guid shipmentId)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new DomainException("Shipment id cannot be empty.");
        }

        TrackingCode = Guard.Required(trackingCode, nameof(TrackingCode));
        ShipmentId = shipmentId;
        Touch();
    }

    public OrderStatusHistory RecordCreated(Guid historyId, string? note, string? changedBy)
    {
        if (_statusHistory.Any(history => history.FromStatus is null && history.ToStatus == OrderStatus.Pending))
        {
            throw new DomainException("Order creation status history has already been recorded.");
        }

        var history = new OrderStatusHistory(historyId, Id, null, OrderStatus.Pending, note, changedBy);
        _statusHistory.Add(history);

        return history;
    }

    public OrderStatusHistory ChangeStatus(
        Guid historyId,
        OrderStatus toStatus,
        string? note,
        string? changedBy)
    {
        if (Status == toStatus)
        {
            throw new DomainException("Order status is already set.");
        }

        if (!CanTransition(Status, toStatus))
        {
            throw new DomainException($"Order status cannot change from {Status} to {toStatus}.");
        }

        var fromStatus = Status;
        Status = toStatus;
        Touch();

        var history = new OrderStatusHistory(historyId, Id, fromStatus, toStatus, note, changedBy);
        _statusHistory.Add(history);

        return history;
    }

    private static bool CanTransition(OrderStatus fromStatus, OrderStatus toStatus)
    {
        return fromStatus switch
        {
            OrderStatus.Pending => toStatus is OrderStatus.Confirmed or OrderStatus.Cancelled,
            OrderStatus.Confirmed => toStatus is OrderStatus.Processing or OrderStatus.Cancelled,
            OrderStatus.Processing => toStatus == OrderStatus.Shipping,
            OrderStatus.Shipping => toStatus is OrderStatus.Completed or OrderStatus.FailedDelivery,
            OrderStatus.FailedDelivery => toStatus is OrderStatus.Shipping or OrderStatus.Cancelled,
            OrderStatus.Completed => toStatus == OrderStatus.Returned,
            _ => false
        };
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Sum(item => item.LineTotal);
        TotalAmount = Subtotal + ShippingFee - DiscountAmount;
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static Guid? NormalizeCustomerId(Guid? customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Order customer id cannot be empty.");
        }

        return customerId;
    }
}
