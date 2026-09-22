using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Ordering;

public sealed class OrderStatusHistory : Entity
{
    public OrderStatusHistory(
        Guid id,
        Guid orderId,
        OrderStatus? fromStatus,
        OrderStatus toStatus,
        string? note,
        string? changedBy)
        : this(id, orderId, fromStatus, toStatus, note, cancellationReason: null, customerMessage: null, changedBy: changedBy)
    {
    }

    public OrderStatusHistory(
        Guid id,
        Guid orderId,
        OrderStatus? fromStatus,
        OrderStatus toStatus,
        string? internalNote,
        string? cancellationReason,
        string? customerMessage,
        string? changedBy)
        : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order status history order id cannot be empty.");
        }

        OrderId = orderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Note = Guard.Optional(internalNote);
        CancellationReason = Guard.Optional(cancellationReason);
        CustomerMessage = Guard.Optional(customerMessage);
        ChangedBy = Guard.Optional(changedBy);
        ChangedAt = DateTimeOffset.UtcNow;
    }

    public Guid OrderId { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public string? Note { get; private set; }

    public string? CancellationReason { get; private set; }

    public string? CustomerMessage { get; private set; }

    public string? ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }
}
