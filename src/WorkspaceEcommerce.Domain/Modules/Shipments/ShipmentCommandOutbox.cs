using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public sealed class ShipmentCommandOutbox : Entity
{
    private ShipmentCommandOutbox()
    {
    }

    public ShipmentCommandOutbox(
        Guid id,
        Guid orderId,
        ShipmentCommandType commandType,
        string? reason,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order id is required for a shipment command.");
        }

        if (createdAtUtc == default)
        {
            throw new DomainException("Shipment command created timestamp is required.");
        }

        OrderId = orderId;
        CommandType = commandType;
        Reason = Guard.Optional(reason);
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    public Guid OrderId { get; private set; }

    public ShipmentCommandType CommandType { get; private set; }

    public string? Reason { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public void ScheduleRetry(string error, DateTimeOffset nextAttemptAtUtc)
    {
        if (nextAttemptAtUtc == default)
        {
            throw new DomainException("Shipment command retry timestamp is required.");
        }

        AttemptCount++;
        LastError = Guard.Required(error, nameof(error));
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    public void MarkCompleted(DateTimeOffset completedAtUtc)
    {
        if (completedAtUtc == default)
        {
            throw new DomainException("Shipment command completed timestamp is required.");
        }

        AttemptCount++;
        CompletedAtUtc = completedAtUtc;
        LastError = null;
    }
}

public enum ShipmentCommandType
{
    Create = 0,
    Cancel = 1
}
