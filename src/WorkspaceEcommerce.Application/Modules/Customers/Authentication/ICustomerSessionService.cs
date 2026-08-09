using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public interface ICustomerSessionService
{
    Task<CustomerAuthResponse> IssueAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<Result<CustomerAuthResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, string reason, CancellationToken cancellationToken = default);

    Task RevokeAllAsync(Guid customerId, string reason, CancellationToken cancellationToken = default);
}
