namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed record CustomerRegisterRequest(
    string FullName,
    string PhoneNumber,
    string Email,
    string Password);
