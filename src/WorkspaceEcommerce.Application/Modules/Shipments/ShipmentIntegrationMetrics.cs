using System.Diagnostics.Metrics;
using WorkspaceEcommerce.Domain.Modules.Shipments;

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
    private static readonly Counter<long> ShippingRequests = Meter.CreateCounter<long>("shipment.checkout.requests");
    private static readonly Histogram<double> ShippingDuration = Meter.CreateHistogram<double>("shipment.checkout.duration", "s");
    private static readonly Counter<long> CheckoutRejections = Meter.CreateCounter<long>("shipment.checkout.quote_rejections");

    public static void RecordShippingRequest(string operation, ShippingMethod? method, string outcome, double seconds)
    {
        // Call sites supply fixed operations and ResultStatus; no IDs, payloads or error text.
        KeyValuePair<string, object?>[] tags = [new("operation", operation), new("method", Method(method)), new("outcome", outcome)];
        ShippingRequests.Add(1, tags);
        ShippingDuration.Record(seconds, tags);
    }

    public static void RecordCheckoutRejection(ShippingMethod method, string reason) =>
        CheckoutRejections.Add(1, new("method", Method(method)), new("reason", reason));

    private static string Method(ShippingMethod? method) => method switch
    {
        ShippingMethod.WorkspaceEC => "WorkspaceEC",
        ShippingMethod.MiniLogistics => "MiniLogistics",
        _ => "Unknown"
    };

    public static void RecordWebhookReject() => WebhookRejects.Add(1);

    public static void RecordDuplicateWebhook() => DuplicateWebhooks.Add(1);

    public static void RecordQuoteFailure() => QuoteFailures.Add(1);

    public static void RecordCreateFailure() => CreateFailures.Add(1);

    public static void RecordTrackingRefreshFailure() => TrackingRefreshFailures.Add(1);

    public static void RecordCancelFailure() => CancelFailures.Add(1);
}
