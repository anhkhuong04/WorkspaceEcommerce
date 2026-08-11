using System.Diagnostics.Metrics;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

/// <summary>
/// Low-cardinality warranty metrics. Do not add identifiers, order numbers,
/// customer IDs, or free-text errors as tags.
/// </summary>
internal static class WarrantyMetrics
{
    private static readonly Meter Meter = new("WorkspaceEcommerce.Warranty", "1.0.0");
    private static readonly Counter<long> LookupCounter = Meter.CreateCounter<long>("warranty.lookup.count");
    private static readonly Counter<long> ActivationCounter = Meter.CreateCounter<long>("warranty.activation.count");
    private static readonly Counter<long> ImportCounter = Meter.CreateCounter<long>("warranty.import.rows");

    public static void RecordLookup(bool found) => LookupCounter.Add(1, new KeyValuePair<string, object?>("outcome", found ? "found" : "not_found"));

    public static void RecordActivation(string outcome, string source) => ActivationCounter.Add(1,
        new KeyValuePair<string, object?>("outcome", outcome),
        new KeyValuePair<string, object?>("source", source));

    public static void RecordImport(int rows, bool committed, bool valid)
    {
        if (rows > 0)
        {
            ImportCounter.Add(rows,
                new KeyValuePair<string, object?>("committed", committed),
                new KeyValuePair<string, object?>("valid", valid));
        }
    }
}
