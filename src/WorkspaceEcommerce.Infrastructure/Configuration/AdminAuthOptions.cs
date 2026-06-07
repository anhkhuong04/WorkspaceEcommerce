namespace WorkspaceEcommerce.Infrastructure.Configuration;

internal sealed class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    public required string Email { get; init; }

    public required string Password { get; init; }
}
