using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public interface ICustomerAuthService
{
    Task<Result<CustomerAuthResponse>> RegisterAsync(
        CustomerRegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAuthResponse>> LoginAsync(
        CustomerLoginRequest request,
        CancellationToken cancellationToken = default);
}
