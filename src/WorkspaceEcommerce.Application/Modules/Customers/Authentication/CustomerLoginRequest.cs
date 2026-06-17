namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed record CustomerLoginRequest(
    string Email,
    string Password);
