using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public interface ICustomerAccountLifecycleService
{
    Task QueueEmailVerificationAsync(Customer customer, CancellationToken cancellationToken = default);

    Task RevokeOutstandingPasswordResetTokensAsync(
        Guid customerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<Result> RequestEmailVerificationAsync(
        RequestEmailVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ConfirmEmailVerificationAsync(
        ConfirmEmailVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
