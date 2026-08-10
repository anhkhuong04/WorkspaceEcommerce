using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace WorkspaceEcommerce.Application.Modules.Operations;

/// <summary>
/// Provider-neutral metrics for durable background work. Queue snapshots are
/// emitted as gauges and are intentionally tagged only with the outbox name,
/// never customer, recipient, payload, or provider response data.
/// </summary>
public static class OutboxProcessingMetrics
{
    private static readonly Meter Meter = new("WorkspaceEcommerce.Outbox", "1.0.0");
    private static readonly Counter<long> Claimed = Meter.CreateCounter<long>(
        "workspaceecommerce.outbox.claimed",
        unit: "messages",
        description: "Durable outbox messages claimed by a worker.");
    private static readonly Counter<long> Completed = Meter.CreateCounter<long>(
        "workspaceecommerce.outbox.completed",
        unit: "messages",
        description: "Durable outbox messages completed by a worker.");
    private static readonly Counter<long> Retried = Meter.CreateCounter<long>(
        "workspaceecommerce.outbox.retried",
        unit: "messages",
        description: "Durable outbox messages scheduled for retry.");
    private static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>(
        "workspaceecommerce.outbox.dead_lettered",
        unit: "messages",
        description: "Durable outbox messages moved to a terminal dead-letter state.");
    private static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>(
        "workspaceecommerce.outbox.processing.duration",
        unit: "s",
        description: "Elapsed time spent attempting one durable outbox message.");
    private static readonly ConcurrentDictionary<string, QueueSnapshot> Snapshots = new(StringComparer.Ordinal);

    static OutboxProcessingMetrics()
    {
        Meter.CreateObservableGauge<long>(
            "workspaceecommerce.outbox.due",
            ObserveDue,
            unit: "messages",
            description: "Durable outbox items currently eligible for a worker claim.");
        Meter.CreateObservableGauge<long>(
            "workspaceecommerce.outbox.leased",
            ObserveLeased,
            unit: "messages",
            description: "Durable outbox items with an active worker lease.");
        Meter.CreateObservableGauge<long>(
            "workspaceecommerce.outbox.retrying",
            ObserveRetrying,
            unit: "messages",
            description: "Active durable outbox items with at least one delivery attempt.");
        Meter.CreateObservableGauge<long>(
            "workspaceecommerce.outbox.dead_letter",
            ObserveDeadLetters,
            unit: "messages",
            description: "Terminal durable outbox items awaiting operator action.");
        Meter.CreateObservableGauge<double>(
            "workspaceecommerce.outbox.oldest_active.age",
            ObserveOldestAge,
            unit: "s",
            description: "Age of the oldest active durable outbox item.");
    }

    public static void RecordClaim(string outbox, int count)
    {
        if (count > 0)
        {
            Claimed.Add(count, OutboxTag(outbox));
        }
    }

    public static void RecordCompleted(string outbox) => Completed.Add(1, OutboxTag(outbox));

    public static void RecordRetry(string outbox) => Retried.Add(1, OutboxTag(outbox));

    public static void RecordDeadLetter(string outbox) => DeadLettered.Add(1, OutboxTag(outbox));

    public static void RecordProcessingDuration(string outbox, TimeSpan duration) =>
        ProcessingDuration.Record(Math.Max(0d, duration.TotalSeconds), OutboxTag(outbox));

    public static void RecordSnapshot(OutboxQueueSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshots[snapshot.Outbox] = new QueueSnapshot(
            snapshot.DueCount,
            snapshot.LeasedCount,
            snapshot.RetryCount,
            snapshot.DeadLetterCount,
            snapshot.OldestActiveAgeSeconds ?? 0d);
    }

    private static KeyValuePair<string, object?> OutboxTag(string outbox) =>
        new("outbox", outbox);

    private static IEnumerable<Measurement<long>> ObserveDue() =>
        Snapshots.Select(pair => new Measurement<long>(pair.Value.DueCount, OutboxTag(pair.Key)));

    private static IEnumerable<Measurement<long>> ObserveLeased() =>
        Snapshots.Select(pair => new Measurement<long>(pair.Value.LeasedCount, OutboxTag(pair.Key)));

    private static IEnumerable<Measurement<long>> ObserveRetrying() =>
        Snapshots.Select(pair => new Measurement<long>(pair.Value.RetryCount, OutboxTag(pair.Key)));

    private static IEnumerable<Measurement<long>> ObserveDeadLetters() =>
        Snapshots.Select(pair => new Measurement<long>(pair.Value.DeadLetterCount, OutboxTag(pair.Key)));

    private static IEnumerable<Measurement<double>> ObserveOldestAge() =>
        Snapshots.Select(pair => new Measurement<double>(pair.Value.OldestAgeSeconds, OutboxTag(pair.Key)));

    private sealed record QueueSnapshot(
        long DueCount,
        long LeasedCount,
        long RetryCount,
        long DeadLetterCount,
        double OldestAgeSeconds);
}
