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
        Status = ShipmentCommandStatus.Pending;
    }

    public Guid OrderId { get; private set; }

    public ShipmentCommandType CommandType { get; private set; }

    public string? Reason { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public ShipmentCommandStatus Status { get; private set; }

    public string? LeaseOwner { get; private set; }

    public Guid? LeaseToken { get; private set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }

    public string? LastErrorCategory { get; private set; }

    public bool IsDueAt(DateTimeOffset timestamp) =>
        Status is ShipmentCommandStatus.Pending or ShipmentCommandStatus.Leased &&
        CompletedAtUtc is null &&
        DeadLetteredAtUtc is null &&
        NextAttemptAtUtc <= timestamp &&
        (LeaseExpiresAtUtc is null || LeaseExpiresAtUtc <= timestamp);

    public void Claim(
        string leaseOwner,
        Guid leaseToken,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new DomainException("Shipment command lease owner is required.");
        }

        if (leaseToken == Guid.Empty)
        {
            throw new DomainException("Shipment command lease token cannot be empty.");
        }

        if (leaseExpiresAtUtc <= claimedAtUtc)
        {
            throw new DomainException("Shipment command lease must expire after it is claimed.");
        }

        if (!IsDueAt(claimedAtUtc))
        {
            throw new DomainException("Only due shipment commands can be claimed.");
        }

        AttemptCount++;
        LastAttemptAtUtc = claimedAtUtc;
        LeaseOwner = leaseOwner.Trim();
        LeaseToken = leaseToken;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        Status = ShipmentCommandStatus.Leased;
    }

    public void ScheduleRetry(string error, DateTimeOffset nextAttemptAtUtc)
    {
        EnsureNotTerminal();
        EnsureNotLeased();
        ScheduleRetryCore(error, nextAttemptAtUtc);
    }

    public void ScheduleRetry(
        Guid leaseToken,
        string error,
        string errorCategory,
        DateTimeOffset nextAttemptAtUtc)
    {
        EnsureLeaseOwnership(leaseToken);
        ScheduleRetryCore(error, nextAttemptAtUtc);
        LastErrorCategory = Guard.Required(errorCategory, nameof(errorCategory));
    }

    private void ScheduleRetryCore(string error, DateTimeOffset nextAttemptAtUtc)
    {
        if (nextAttemptAtUtc == default)
        {
            throw new DomainException("Shipment command retry timestamp is required.");
        }

        LastError = Guard.Required(error, nameof(error));
        NextAttemptAtUtc = nextAttemptAtUtc;
        Status = ShipmentCommandStatus.Pending;
        ClearLease();
    }

    public void MarkCompleted(DateTimeOffset completedAtUtc)
    {
        EnsureNotTerminal();
        EnsureNotLeased();
        MarkCompletedCore(completedAtUtc);
    }

    public void MarkCompleted(Guid leaseToken, DateTimeOffset completedAtUtc)
    {
        EnsureLeaseOwnership(leaseToken);
        MarkCompletedCore(completedAtUtc);
    }

    private void MarkCompletedCore(DateTimeOffset completedAtUtc)
    {
        if (completedAtUtc == default)
        {
            throw new DomainException("Shipment command completed timestamp is required.");
        }

        CompletedAtUtc = completedAtUtc;
        LastError = null;
        LastErrorCategory = null;
        Status = ShipmentCommandStatus.Completed;
        ClearLease();
    }

    public void DeadLetter(
        Guid leaseToken,
        string error,
        string errorCategory,
        DateTimeOffset deadLetteredAtUtc)
    {
        EnsureLeaseOwnership(leaseToken);
        if (deadLetteredAtUtc == default)
        {
            throw new DomainException("Shipment command dead-letter timestamp is required.");
        }

        LastError = Guard.Required(error, nameof(error));
        LastErrorCategory = Guard.Required(errorCategory, nameof(errorCategory));
        DeadLetteredAtUtc = deadLetteredAtUtc;
        Status = ShipmentCommandStatus.DeadLetter;
        ClearLease();
    }

    private void EnsureNotTerminal()
    {
        if (Status is ShipmentCommandStatus.Completed or ShipmentCommandStatus.DeadLetter)
        {
            throw new DomainException("A terminal shipment command cannot be changed.");
        }
    }

    private void EnsureNotLeased()
    {
        if (LeaseToken is not null)
        {
            throw new DomainException("An active shipment command lease is required to change this command.");
        }
    }

    private void EnsureLeaseOwnership(Guid leaseToken)
    {
        if (leaseToken == Guid.Empty || LeaseToken != leaseToken)
        {
            throw new DomainException("Shipment command lease is not owned by this worker.");
        }

        EnsureNotTerminal();
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseToken = null;
        LeaseExpiresAtUtc = null;
    }
}

public enum ShipmentCommandType
{
    Create = 0,
    Cancel = 1
}

public enum ShipmentCommandStatus
{
    Pending = 0,
    Leased = 1,
    Completed = 2,
    DeadLetter = 3
}
