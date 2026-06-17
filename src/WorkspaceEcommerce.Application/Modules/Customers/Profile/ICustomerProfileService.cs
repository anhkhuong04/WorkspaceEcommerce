using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

public interface ICustomerProfileService
{
    Task<Result<CustomerProfileDto>> GetMeAsync(CancellationToken cancellationToken = default);

    Task<Result<CustomerProfileDto>> UpdateMeAsync(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default);
}
