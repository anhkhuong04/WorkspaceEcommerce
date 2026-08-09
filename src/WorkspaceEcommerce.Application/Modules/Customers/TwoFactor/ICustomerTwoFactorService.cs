using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;

public interface ICustomerTwoFactorService
{
    Task<Result<TwoFactorSetupStartResponse>> StartSetupAsync(CancellationToken cancellationToken = default);

    Task<Result<TwoFactorSetupConfirmationResponse>> ConfirmSetupAsync(
        ConfirmTwoFactorSetupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DisableAsync(
        DisableTwoFactorRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerAuthResponse?> CreateLoginChallengeAsync(
        Customer customer,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAuthResponse>> VerifyLoginAsync(
        VerifyTwoFactorLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAuthResponse>> VerifyRecoveryAsync(
        VerifyTwoFactorRecoveryRequest request,
        CancellationToken cancellationToken = default);
}
