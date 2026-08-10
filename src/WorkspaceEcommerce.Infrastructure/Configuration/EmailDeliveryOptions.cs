namespace WorkspaceEcommerce.Infrastructure.Configuration;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    public string Provider { get; init; } = "Log";

    public string SenderEmail { get; init; } = "";

    public string? Host { get; init; }

    public int Port { get; init; } = 587;

    public bool EnableSsl { get; init; } = true;

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public int WorkerIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Maximum number of outbox messages a worker claims per poll. Keeping the
    /// batch bounded prevents a slow provider from holding an unbounded amount
    /// of work under lease.
    /// </summary>
    public int WorkerBatchSize { get; init; } = 20;

    /// <summary>
    /// Duration for which a worker owns a claimed message. Another replica can
    /// reclaim the message when this lease expires after a crash.
    /// </summary>
    public int LeaseDurationSeconds { get; init; } = 120;

    /// <summary>
    /// Total delivery attempts before a message becomes a terminal dead-letter
    /// record that requires operator review.
    /// </summary>
    public int MaxDeliveryAttempts { get; init; } = 8;
}
