using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using WorkspaceEcommerce.Api.Observability;

namespace WorkspaceEcommerce.Api.IntegrationTests.Observability;

public sealed class SensitiveTelemetryRedactionProcessorTests
{
    [Fact]
    public void Process_RequestTelemetry_RedactsCredentialsAndQueryValuesWhileRetainingSafeProperties()
    {
        var sink = new CapturingTelemetryProcessor();
        var processor = new SensitiveTelemetryRedactionProcessor(sink);
        var request = new RequestTelemetry
        {
            Url = new Uri("https://api.example.test/v1/orders?coupon=SUMMER25&refresh_token=token-value#fragment"),
            Name = "GET /v1/orders?coupon=SUMMER25"
        };
        request.Properties["Authorization"] = "Bearer very-secret-token";
        request.Properties["Cookie"] = "session=customer-session";
        request.Properties["refresh_token"] = "token-value";
        request.Properties["customerEmail"] = "customer@example.com";
        request.Properties["webhookSignature"] = "signature-value";
        request.Properties["orderId"] = "order-42";
        request.Properties["statusCode"] = "200";

        processor.Process(request);

        Assert.Same(request, Assert.Single(sink.Items));
        Assert.Equal(string.Empty, request.Url!.Query);
        Assert.Equal(string.Empty, request.Url.Fragment);
        Assert.DoesNotContain("SUMMER25", request.Name, StringComparison.Ordinal);
        Assert.Equal(SensitiveTelemetryRedactionProcessor.RedactedValue, request.Properties["Authorization"]);
        Assert.Equal(SensitiveTelemetryRedactionProcessor.RedactedValue, request.Properties["Cookie"]);
        Assert.Equal(SensitiveTelemetryRedactionProcessor.RedactedValue, request.Properties["refresh_token"]);
        Assert.Equal(SensitiveTelemetryRedactionProcessor.RedactedValue, request.Properties["customerEmail"]);
        Assert.Equal(SensitiveTelemetryRedactionProcessor.RedactedValue, request.Properties["webhookSignature"]);
        Assert.Equal("order-42", request.Properties["orderId"]);
        Assert.Equal("200", request.Properties["statusCode"]);
    }

    [Fact]
    public void Process_TraceDependencyAndException_RedactsEmbeddedSecrets()
    {
        var sink = new CapturingTelemetryProcessor();
        var processor = new SensitiveTelemetryRedactionProcessor(sink);
        var trace = new TraceTelemetry(
            "callback Authorization: Bearer very-secret-token; Cookie=session-value; customer@example.com https://api.example.test/callback?code=oauth-code");
        var dependency = new DependencyTelemetry
        {
            Name = "POST https://carrier.example.test/shipments?api_key=carrier-key",
            Data = "https://carrier.example.test/shipments?api_key=carrier-key",
            Target = "https://carrier.example.test/shipments?api_key=carrier-key"
        };
        var exception = new ExceptionTelemetry(new InvalidOperationException("refresh_token=refresh-secret"));

        processor.Process(trace);
        processor.Process(dependency);
        processor.Process(exception);

        Assert.DoesNotContain("very-secret-token", trace.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("session-value", trace.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("oauth-code", trace.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("customer@example.com", trace.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("carrier-key", dependency.Name, StringComparison.Ordinal);
        Assert.DoesNotContain("carrier-key", dependency.Data, StringComparison.Ordinal);
        Assert.DoesNotContain("carrier-key", dependency.Target, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-secret", exception.Message, StringComparison.Ordinal);
        Assert.All(exception.ExceptionDetailsInfoList, detail =>
            Assert.DoesNotContain("refresh-secret", detail.Message, StringComparison.Ordinal));
    }

    private sealed class CapturingTelemetryProcessor : ITelemetryProcessor
    {
        public List<ITelemetry> Items { get; } = [];

        public void Process(ITelemetry item) => Items.Add(item);
    }
}
