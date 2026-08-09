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
}
