namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

public sealed record CustomerProfileDto(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
