namespace WorkspaceEcommerce.Infrastructure.Shipment;

public sealed class MiniLogisticsOptions
{
    public const string SectionName = "MiniLogistics";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;

    public int WebhookToleranceSeconds { get; init; } = 300;

    public int OperationTimeoutSeconds { get; init; } = 10;

    public int MaxRetryAttempts { get; init; } = 2;

    public int RetryBaseDelayMilliseconds { get; init; } = 250;

    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    public int CircuitBreakerBreakSeconds { get; init; } = 30;

    public int CommandWorkerIntervalSeconds { get; init; } = 15;
}
