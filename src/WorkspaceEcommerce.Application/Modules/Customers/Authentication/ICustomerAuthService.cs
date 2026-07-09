using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public interface ICustomerAuthService
{
    Task<Result<CustomerAuthResponse>> RegisterAsync(
        CustomerRegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAuthResponse>> LoginAsync(
        CustomerLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAuthResponse>> LoginWithGoogleAsync(
        CustomerGoogleLoginRequest request,
        CancellationToken cancellationToken = default);
}


