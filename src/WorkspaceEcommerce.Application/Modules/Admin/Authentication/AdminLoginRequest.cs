namespace WorkspaceEcommerce.Application.Modules.Admin.Authentication;

public sealed record AdminLoginRequest(
    string Email,
    string Password);
