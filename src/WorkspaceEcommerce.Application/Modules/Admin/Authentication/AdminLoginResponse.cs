namespace WorkspaceEcommerce.Application.Modules.Admin.Authentication;

public sealed record AdminLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string Email);
