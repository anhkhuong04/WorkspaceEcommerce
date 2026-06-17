namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed record CustomerAuthResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    Guid CustomerId,
    string Email,
    string FullName,
    string PhoneNumber);
