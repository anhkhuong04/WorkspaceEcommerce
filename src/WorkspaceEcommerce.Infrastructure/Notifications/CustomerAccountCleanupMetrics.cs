using System.Diagnostics.Metrics;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

internal static class CustomerAccountCleanupMetrics
{
    internal const string MeterName = "WorkspaceEcommerce.CustomerAccountCleanup";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> DeletedRows = Meter.CreateCounter<long>(
        "workspaceecommerce.customer_account_cleanup.deleted",
        unit: "rows",
        description: "Expired customer-account rows deleted by the cleanup worker.");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>(
        "workspaceecommerce.customer_account_cleanup.failures",
        unit: "failures",
        description: "Customer-account cleanup cycles that failed.");
    private static readonly Counter<long> BudgetExhausted = Meter.CreateCounter<long>(
        "workspaceecommerce.customer_account_cleanup.budget_exhausted",
        unit: "cycles",
        description: "Cleanup cycles that stopped with backlog because their time budget was exhausted.");
    private static readonly Histogram<double> CycleDuration = Meter.CreateHistogram<double>(
        "workspaceecommerce.customer_account_cleanup.duration",
        unit: "s",
        description: "Elapsed customer-account cleanup cycle time.");

    public static void RecordDeleted(string dataset, int count)
    {
        if (count > 0)
        {
            DeletedRows.Add(count, DatasetTag(dataset));
        }
    }

    public static void RecordCompleted(TimeSpan duration, bool timeBudgetExhausted)
    {
        CycleDuration.Record(Math.Max(0d, duration.TotalSeconds), OutcomeTag(
            timeBudgetExhausted ? "budget_exhausted" : "drained"));
        if (timeBudgetExhausted)
        {
            BudgetExhausted.Add(1);
        }
    }

    public static void RecordLockUnavailable(TimeSpan duration) =>
        CycleDuration.Record(Math.Max(0d, duration.TotalSeconds), OutcomeTag("lock_unavailable"));

    public static void RecordFailure(TimeSpan duration)
    {
        Failures.Add(1);
        CycleDuration.Record(Math.Max(0d, duration.TotalSeconds), OutcomeTag("failed"));
    }

    private static KeyValuePair<string, object?> DatasetTag(string dataset) =>
        new("dataset", dataset);

    private static KeyValuePair<string, object?> OutcomeTag(string outcome) =>
        new("outcome", outcome);
}
