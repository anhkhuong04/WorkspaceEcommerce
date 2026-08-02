using System.Diagnostics.Metrics;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public static class ShipmentIntegrationMetrics
{
    private static readonly Meter Meter = new("WorkspaceEcommerce.Shipments", "1.0.0");
    private static readonly Counter<long> WebhookRejects = Meter.CreateCounter<long>("shipment.webhook.rejects");
    private static readonly Counter<long> DuplicateWebhooks = Meter.CreateCounter<long>("shipment.webhook.duplicates");
    private static readonly Counter<long> QuoteFailures = Meter.CreateCounter<long>("shipment.quote.failures");
    private static readonly Counter<long> CreateFailures = Meter.CreateCounter<long>("shipment.create.failures");
    private static readonly Counter<long> TrackingRefreshFailures = Meter.CreateCounter<long>("shipment.tracking.refresh.failures");
    private static readonly Counter<long> CancelFailures = Meter.CreateCounter<long>("shipment.cancel.failures");

    public static void RecordWebhookReject() => WebhookRejects.Add(1);

    public static void RecordDuplicateWebhook() => DuplicateWebhooks.Add(1);

    public static void RecordQuoteFailure() => QuoteFailures.Add(1);

    public static void RecordCreateFailure() => CreateFailures.Add(1);

    public static void RecordTrackingRefreshFailure() => TrackingRefreshFailures.Add(1);

    public static void RecordCancelFailure() => CancelFailures.Add(1);
}
