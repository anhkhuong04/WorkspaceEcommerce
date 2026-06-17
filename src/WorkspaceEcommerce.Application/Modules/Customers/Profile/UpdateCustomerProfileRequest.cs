namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

public sealed record UpdateCustomerProfileRequest(
    string FullName,
    string PhoneNumber);
