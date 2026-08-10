using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

/// <summary>
/// Durable email command. The serialized email body is Data Protection
/// protected before it reaches this entity, so the database never holds raw
/// account-recovery credentials.
/// </summary>
public sealed class CustomerEmailOutboxMessage : Entity
{
    private CustomerEmailOutboxMessage()
    {
    }

    public CustomerEmailOutboxMessage(
        Guid id,
        string recipientEmail,
        string subject,
        string protectedPayload,
        DateTimeOffset createdAt)
        : base(id)
    {
        RecipientEmail = Guard.Required(recipientEmail, nameof(recipientEmail));
        Subject = Guard.Required(subject, nameof(subject));
        ProtectedPayload = Guard.Required(protectedPayload, nameof(protectedPayload));
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
        Status = CustomerEmailOutboxStatus.Pending;
    }

    public string RecipientEmail { get; private set; } = default!;

    public string Subject { get; private set; } = default!;

    public string ProtectedPayload { get; private set; } = default!;

    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public CustomerEmailOutboxStatus Status { get; private set; }

    public string? LeaseOwner { get; private set; }

    /// <summary>
    /// A short-lived ownership token assigned by an outbox worker. It is also
    /// used as an optimistic-concurrency token so a worker whose lease has
    /// expired cannot complete work claimed by a newer worker.
    /// </summary>
    public Guid? LeaseToken { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    /// <summary>
    /// Terminal delivery failures are retained for operator review rather than
    /// being retried forever.
    /// </summary>
    public DateTimeOffset? DeadLetteredAt { get; private set; }

    public bool IsDueAt(DateTimeOffset timestamp) =>
        Status is CustomerEmailOutboxStatus.Pending or CustomerEmailOutboxStatus.Leased &&
        SentAt is null &&
        DeadLetteredAt is null &&
        NextAttemptAt <= timestamp &&
        (LeaseExpiresAt is null || LeaseExpiresAt <= timestamp);

    public void Claim(
        string leaseOwner,
        Guid leaseToken,
        DateTimeOffset claimedAt,
        DateTimeOffset leaseExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new DomainException("Email outbox lease owner is required.");
        }

        if (leaseToken == Guid.Empty)
        {
            throw new DomainException("Email outbox lease token cannot be empty.");
        }

        if (leaseExpiresAt <= claimedAt)
        {
            throw new DomainException("Email outbox lease must expire after it is claimed.");
        }

        if (!IsDueAt(claimedAt))
        {
            throw new DomainException("Only due, unleased email outbox messages can be claimed.");
        }

        LeaseOwner = leaseOwner.Trim();
        LeaseToken = leaseToken;
        LeaseExpiresAt = leaseExpiresAt;
        Status = CustomerEmailOutboxStatus.Leased;
    }

    public void MarkSent(DateTimeOffset sentAt)
    {
        EnsureNotTerminal();
        EnsureNotLeased();
        MarkSentCore(sentAt);
    }

    public void MarkSent(Guid leaseToken, DateTimeOffset sentAt)
    {
        EnsureLeaseOwnership(leaseToken);
        MarkSentCore(sentAt);
    }

    public void ScheduleRetry(string error, DateTimeOffset nextAttemptAt)
    {
        EnsureNotTerminal();
        EnsureNotLeased();
        ScheduleRetryCore(error, nextAttemptAt);
    }

    public void ScheduleRetry(Guid leaseToken, string error, DateTimeOffset nextAttemptAt)
    {
        EnsureLeaseOwnership(leaseToken);
        ScheduleRetryCore(error, nextAttemptAt);
    }

    public void DeadLetter(Guid leaseToken, string error, DateTimeOffset deadLetteredAt)
    {
        EnsureLeaseOwnership(leaseToken);
        AttemptCount++;
        LastError = Guard.Required(error, nameof(error));
        DeadLetteredAt = deadLetteredAt;
        Status = CustomerEmailOutboxStatus.DeadLetter;
        ClearLease();
    }

    private void MarkSentCore(DateTimeOffset sentAt)
    {
        AttemptCount++;
        SentAt = sentAt;
        LastError = null;
        Status = CustomerEmailOutboxStatus.Sent;
        ClearLease();
    }

    private void ScheduleRetryCore(string error, DateTimeOffset nextAttemptAt)
    {
        if (nextAttemptAt <= NextAttemptAt)
        {
            throw new DomainException("Email retry must be scheduled in the future.");
        }

        AttemptCount++;
        LastError = Guard.Required(error, nameof(error));
        NextAttemptAt = nextAttemptAt;
        Status = CustomerEmailOutboxStatus.Pending;
        ClearLease();
    }

    private void EnsureNotTerminal()
    {
        if (SentAt is not null || DeadLetteredAt is not null)
        {
            throw new DomainException("A completed email outbox message cannot be changed.");
        }
    }

    private void EnsureLeaseOwnership(Guid leaseToken)
    {
        if (leaseToken == Guid.Empty || LeaseToken != leaseToken)
        {
            throw new DomainException("Email outbox lease is not owned by this worker.");
        }

        EnsureNotTerminal();
    }

    private void EnsureNotLeased()
    {
        if (LeaseToken is not null)
        {
            throw new DomainException("An active email outbox lease is required to change this message.");
        }
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseToken = null;
        LeaseExpiresAt = null;
    }
}

public enum CustomerEmailOutboxStatus
{
    Pending = 0,
    Leased = 1,
    Sent = 2,
    DeadLetter = 3
}
