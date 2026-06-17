using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;

namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

public interface IJwtTokenGenerator
{
    AdminLoginResponse GenerateAdminToken(string email);

    CustomerAuthResponse GenerateCustomerToken(
        Guid customerId,
        string email,
        string fullName,
        string phoneNumber);
}
