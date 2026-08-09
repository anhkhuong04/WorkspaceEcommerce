namespace WorkspaceEcommerce.Infrastructure.Configuration;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public bool Enabled { get; init; }

    public string[] AllowedClientIds { get; init; } = [];
}
