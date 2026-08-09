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
    }

    public string RecipientEmail { get; private set; } = default!;

    public string Subject { get; private set; } = default!;

    public string ProtectedPayload { get; private set; } = default!;

    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public bool IsDueAt(DateTimeOffset timestamp) => SentAt is null && NextAttemptAt <= timestamp;

    public void MarkSent(DateTimeOffset sentAt)
    {
        AttemptCount++;
        SentAt = sentAt;
        LastError = null;
    }

    public void ScheduleRetry(string error, DateTimeOffset nextAttemptAt)
    {
        if (nextAttemptAt <= NextAttemptAt)
        {
            throw new DomainException("Email retry must be scheduled in the future.");
        }

        AttemptCount++;
        LastError = Guard.Required(error, nameof(error));
        NextAttemptAt = nextAttemptAt;
    }
}
