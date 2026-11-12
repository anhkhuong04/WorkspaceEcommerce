using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Ordering;

public enum OrderReturnStatus { AwaitingReceipt = 0, Rejected = 1, Received = 2, Refunded = 3, ClosedWithoutRefund = 4 }

public sealed class OrderReturnCase : Entity
{
    public OrderReturnCase(Guid id, Guid orderId, Guid requestId, string requestFingerprint, string reason, string createdBy, DateTimeOffset createdAt) : base(id)
    {
        if (orderId == Guid.Empty || requestId == Guid.Empty || createdAt == default) throw new DomainException("Return order, request and timestamp are required.");
        OrderId = orderId; RequestId = requestId; RequestFingerprint = Guard.Required(requestFingerprint, nameof(RequestFingerprint));
        Reason = RequiredReason(reason); CreatedBy = Guard.Required(createdBy, nameof(CreatedBy)); CreatedAt = createdAt; Revision = 1;
    }
    public Guid OrderId { get; private set; }
    public Guid RequestId { get; private set; }
    public string RequestFingerprint { get; private set; }
    public string Reason { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public OrderReturnStatus Status { get; private set; }
    public int Revision { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public string? ReceiptFingerprint { get; private set; }
    public decimal MerchandiseRefund { get; private set; }
    public decimal ShippingRefund { get; private set; }
    public decimal RefundAmount => MerchandiseRefund + ShippingRefund;
    public DateTimeOffset? RefundedAt { get; private set; }
    public string? RefundReference { get; private set; }
    public string? RefundActor { get; private set; }
    public string? RefundReason { get; private set; }
    public string? RejectionReason { get; private set; }
    public void Reject(string reason)
    {
        if (Status != OrderReturnStatus.AwaitingReceipt) throw new DomainException("Only a return awaiting receipt can be rejected.");
        RejectionReason = RequiredReason(reason); Status = OrderReturnStatus.Rejected; Revision++;
    }
    public void Receive(decimal merchandiseRefund, decimal shippingRefund, string fingerprint, DateTimeOffset now)
    {
        if (Status != OrderReturnStatus.AwaitingReceipt || now == default) throw new DomainException("This return is no longer awaiting receipt.");
        MerchandiseRefund = CommerceCurrency.RequireValidAmount(merchandiseRefund, nameof(MerchandiseRefund));
        ShippingRefund = CommerceCurrency.RequireValidAmount(shippingRefund, nameof(ShippingRefund));
        ReceiptFingerprint = Guard.Required(fingerprint, nameof(ReceiptFingerprint)); ReceivedAt = now;
        Status = RefundAmount > 0 ? OrderReturnStatus.Received : OrderReturnStatus.ClosedWithoutRefund; Revision++;
    }
    public void RecordRefund(decimal amount, string reference, string reason, string actor, DateTimeOffset now)
    {
        if (Status != OrderReturnStatus.Received || amount != RefundAmount || amount <= 0 || now == default)
            throw new DomainException("Record exactly the approved received-goods refund amount.");
        reference = Guard.Required(reference, nameof(RefundReference));
        if (reference.Length > 120) throw new DomainException("Refund reference is too long.");
        RefundReason = RequiredReason(reason); RefundReference = reference; RefundActor = Guard.Required(actor, nameof(RefundActor));
        RefundedAt = now; Status = OrderReturnStatus.Refunded; Revision++;
    }
    private static string RequiredReason(string reason)
    {
        var value = Guard.Required(reason, "Reason");
        return value.Length <= 500 ? value : throw new DomainException("Reason must not exceed 500 characters.");
    }
}

public sealed class OrderReturnLine : Entity
{
    public OrderReturnLine(Guid id, Guid returnCaseId, Guid orderItemId, int quantity) : base(id)
    {
        if (returnCaseId == Guid.Empty || orderItemId == Guid.Empty || quantity <= 0) throw new DomainException("Return item and positive quantity are required.");
        ReturnCaseId = returnCaseId; OrderItemId = orderItemId; Quantity = quantity;
    }
    public Guid ReturnCaseId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public int Quantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public int RestockQuantity { get; private set; }
    public decimal RefundAmount { get; private set; }
    public string? Inspection { get; private set; }
    public Guid[] SerializedUnitIds { get; private set; } = [];
    public void Inspect(int received, int restock, decimal refund, string inspection, Guid[] unitIds)
    {
        if (received < 0 || received > Quantity || restock < 0 || restock > received || unitIds.Distinct().Count() != unitIds.Length)
            throw new DomainException("Received, restocked or serialized quantities are invalid.");
        var note = Guard.Required(inspection, nameof(Inspection));
        if (note.Length > 500) throw new DomainException("Inspection note must not exceed 500 characters.");
        ReceivedQuantity = received; RestockQuantity = restock; Inspection = note;
        RefundAmount = CommerceCurrency.RequireValidAmount(refund, nameof(RefundAmount)); SerializedUnitIds = unitIds.ToArray();
    }
}

public sealed class OrderReturnAudit : Entity
{
    public OrderReturnAudit(Guid id, Guid returnCaseId, string action, string actor, string reason, DateTimeOffset occurredAt) : base(id)
    {
        ReturnCaseId = returnCaseId; Action = Guard.Required(action, nameof(Action)); Actor = Guard.Required(actor, nameof(Actor));
        Reason = Guard.Required(reason, nameof(Reason)); OccurredAt = occurredAt;
    }
    public Guid ReturnCaseId { get; private set; }
    public string Action { get; private set; }
    public string Actor { get; private set; }
    public string Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
