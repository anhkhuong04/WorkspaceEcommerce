namespace WorkspaceEcommerce.Infrastructure.Shipment;

public sealed class MiniLogisticsOptions
{
    public const string SectionName = "MiniLogistics";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;
}
