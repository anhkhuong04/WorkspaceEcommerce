namespace WorkspaceEcommerce.Application.Modules.Operations;

public sealed record OutboxQueueSnapshotDto(
    string Outbox,
    int DueCount,
    int LeasedCount,
    int RetryCount,
    int DeadLetterCount,
    DateTimeOffset? OldestActiveAt,
    double? OldestActiveAgeSeconds);

public sealed record OutboxOperationsSummaryDto(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyCollection<OutboxQueueSnapshotDto> Queues);

/// <summary>
/// Intentionally excludes recipient, subject, protected payload, and provider
/// response bodies so the operational API does not become a source of PII or
/// credentials.
/// </summary>
public sealed record OutboxDeadLetterDto(
    string Outbox,
    Guid Id,
    Guid? OrderId,
    string? CommandType,
    int AttemptCount,
    string? ErrorCategory,
    string? Error,
    DateTimeOffset DeadLetteredAtUtc);

public sealed record OutboxReplayDto(
    string Outbox,
    Guid SourceId,
    bool Queued);
