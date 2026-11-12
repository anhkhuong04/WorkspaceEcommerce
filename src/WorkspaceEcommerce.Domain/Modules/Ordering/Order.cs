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
        PaymentMethod paymentMethod,
        string currencyCode,
        decimal exchangeRate)
        : base(id)
    {
        OrderCode = Guard.Required(orderCode, nameof(OrderCode));
        CustomerId = NormalizeCustomerId(customerId);
        CustomerName = Guard.Required(customerName, nameof(CustomerName));
        CustomerPhone = Guard.Required(customerPhone, nameof(CustomerPhone));
        CustomerEmail = Guard.Optional(customerEmail);
        ShippingAddress = Guard.Required(shippingAddress, nameof(ShippingAddress));
        Note = Guard.Optional(note);
        PaymentMethod = NormalizePaymentMethod(paymentMethod);
        PaymentStatus = GetInitialPaymentStatus(PaymentMethod);
        CurrencyCode = Guard.Required(currencyCode, nameof(CurrencyCode));
        ExchangeRate = Guard.NotNegative(exchangeRate, nameof(ExchangeRate));
        Status = OrderStatus.Pending;
        ShippingFee = 0m;
        DiscountAmount = 0m;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string OrderCode { get; private set; }

    public string? ImportSource { get; private set; }
    public string? ExternalOrderCode { get; private set; }
    public string? ImportFingerprint { get; private set; }

    public void SetImportIdentity(string source, string externalOrderCode, string fingerprint)
    {
        if (ImportSource is not null || Status != OrderStatus.Pending) throw new DomainException("Import identity can only be assigned once to a new order.");
        if (source.Length is < 1 or > 80 || externalOrderCode.Length is < 1 or > 200 || fingerprint.Length != 64)
            throw new DomainException("Import source, external order code or fingerprint is invalid.");
        ImportSource = source.Trim().ToLowerInvariant();
        ExternalOrderCode = externalOrderCode.Trim().ToUpperInvariant();
        ImportFingerprint = fingerprint;
    }

    public Guid? CustomerId { get; private set; }

    public string CustomerName { get; private set; }

    public string CustomerPhone { get; private set; }

    public string? CustomerEmail { get; private set; }

    public string ShippingAddress { get; private set; }

    public string? ShippingStreet { get; private set; }

    public string? ShippingWard { get; private set; }

    public string? ShippingProvince { get; private set; }

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

    public PaymentStatus PaymentStatus { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>
    /// Immutable business timestamp set when the order first reaches Completed.
    /// Warranty eligibility must use this value rather than infer delivery from
    /// mutable status history at request time.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ReturnRequestedAt { get; private set; }
    public DateTimeOffset? PaymentDueAt { get; private set; }

    public void SetPaymentDeadline(DateTimeOffset dueAt)
    {
        if (PaymentMethod == PaymentMethod.Cod || PaymentDueAt.HasValue || dueAt <= CreatedAt)
            throw new DomainException("A payment deadline can only be frozen once for a prepaid order.");
        PaymentDueAt = dueAt;
    }

    public string? ReturnReason { get; private set; }
    public DateTimeOffset? ReturnReviewedAt { get; private set; }
    public string? ReturnDecision { get; private set; }
    public string? ReturnDecisionReason { get; private set; }
    public decimal RefundedAmount { get; private set; }

    public void ReviewReturnRequest(bool approved, string reason, DateTimeOffset now)
    {
        if (!ReturnRequestedAt.HasValue || ReturnReviewedAt.HasValue) throw new DomainException("No unreviewed return request exists.");
        var note = Guard.Required(reason, nameof(ReturnDecisionReason));
        if (note.Length > 500) throw new DomainException("Return decision reason is too long.");
        ReturnReviewedAt = now; ReturnDecision = approved ? "Approved" : "Rejected"; ReturnDecisionReason = note; Touch();
    }

    public void RecordReturnRefund(decimal amount, bool anotherRefundPending)
    {
        CommerceCurrency.RequireValidAmount(amount, nameof(amount));
        if (PaidAt is null || amount <= 0 || RefundedAmount + amount > TotalAmount)
            throw new DomainException("Refund exceeds the verified paid amount remaining.");
        RefundedAmount += amount;
        PaymentStatus = RefundedAmount == TotalAmount ? PaymentStatus.Refunded : anotherRefundPending ? PaymentStatus.RefundPending : PaymentStatus.PartiallyRefunded;
        Touch();
    }

    public bool RequestReturn(string reason, DateTimeOffset now)
    {
        if (ReturnRequestedAt.HasValue) return false;
        if (Status != OrderStatus.Completed || CompletedAt is null || now < CompletedAt || now > CompletedAt.Value.AddDays(7))
            throw new DomainException("Return requests require a completed order within 7 days of its original completion.");
        var normalizedReason = Guard.Required(reason, nameof(ReturnReason));
        if (normalizedReason.Length > 500) throw new DomainException("Return reason must not exceed 500 characters.");
        ReturnRequestedAt = now;
        ReturnReason = normalizedReason;
        Touch();
        return true;
    }

    public string CurrencyCode { get; private set; }

    public decimal ExchangeRate { get; private set; }

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
        bool requiresInstallation,
        string? productImageUrlSnapshot = null)
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
            requiresInstallation,
            productImageUrlSnapshot);

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
        ShippingFee = CommerceCurrency.RequireValidAmount(shippingFee, nameof(ShippingFee));
        RecalculateTotals();
        Touch();
    }

    public void SetShippingAddressDetails(
        string shippingStreet,
        string shippingWard,
        string shippingProvince)
    {
        ShippingStreet = Guard.Required(shippingStreet, nameof(ShippingStreet));
        ShippingWard = Guard.Required(shippingWard, nameof(ShippingWard));
        ShippingProvince = Guard.Required(shippingProvince, nameof(ShippingProvince));
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

    public void MarkPaymentPending()
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            throw new DomainException("Paid order payment cannot be moved back to pending.");
        }

        PaymentStatus = PaymentStatus.Pending;
        PaidAt = null;
        Touch();
    }

    public void MarkPaymentPaid(DateTimeOffset paidAt)
    {
        if (paidAt == default)
        {
            throw new DomainException("Order paid timestamp is required.");
        }

        PaymentStatus = PaymentStatus.Paid;
        PaidAt = paidAt;
        Touch();
    }

    public void MarkPaymentFailed()
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            throw new DomainException("Paid order payment cannot be marked as failed.");
        }

        PaymentStatus = PaymentStatus.Failed;
        PaidAt = null;
        Touch();
    }

    public void MarkPaymentCancelled()
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            throw new DomainException("Paid order payment cannot be marked as cancelled.");
        }

        PaymentStatus = PaymentStatus.Cancelled;
        PaidAt = null;
        Touch();
    }

    public void MarkPaymentRefundPending()
    {
        if (PaymentStatus == PaymentStatus.RefundPending) return;
        if (PaymentStatus is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
        {
            throw new DomainException("Only a paid order can enter the refund workflow.");
        }

        PaymentStatus = PaymentStatus.RefundPending;
        Touch();
    }

    public void MarkPaymentRefunded()
    {
        if (PaymentStatus != PaymentStatus.RefundPending || Status is not (OrderStatus.Cancelled or OrderStatus.Returned))
            throw new DomainException("A full refund requires a closed order awaiting a refund.");
        PaymentStatus = PaymentStatus.Refunded;
        RefundedAmount = TotalAmount;
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
        return ChangeStatus(historyId, toStatus, note, cancellationReason: null, customerMessage: null, changedBy: changedBy);
    }

    public OrderStatusHistory ChangeStatus(
        Guid historyId,
        OrderStatus toStatus,
        string? internalNote,
        string? cancellationReason,
        string? customerMessage,
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

        if (ImportSource is not null && PaymentStatus != PaymentStatus.Paid &&
            (toStatus == OrderStatus.Completed || toStatus == OrderStatus.Shipping && PaymentMethod != PaymentMethod.Cod))
            throw new DomainException("Imported prepaid delivery requires verified payment; completion requires confirmed collection.");

        var fromStatus = Status;
        Status = toStatus;
        if (toStatus == OrderStatus.Completed && CompletedAt is null)
        {
            CompletedAt = DateTimeOffset.UtcNow;
        }
        Touch();

        var history = new OrderStatusHistory(
            historyId,
            Id,
            fromStatus,
            toStatus,
            internalNote,
            cancellationReason,
            customerMessage,
            changedBy);
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

    private static PaymentMethod NormalizePaymentMethod(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cod or PaymentMethod.ManualBankTransfer or PaymentMethod.VNPay => paymentMethod,
            _ => throw new DomainException("Order payment method is not supported.")
        };
    }

    private static PaymentStatus GetInitialPaymentStatus(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cod => PaymentStatus.Unpaid,
            PaymentMethod.ManualBankTransfer or PaymentMethod.VNPay => PaymentStatus.Pending,
            _ => throw new DomainException("Order payment method is not supported.")
        };
    }
}
