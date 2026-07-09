using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;

namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

public interface ICustomerProfileService
{
    Task<Result<CustomerProfileDto>> GetMeAsync(CancellationToken cancellationToken = default);

    Task<Result<CustomerProfileDto>> UpdateMeAsync(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAccountStatsDto>> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CustomerLoginHistoryDto>>> GetLoginHistoryAsync(CancellationToken cancellationToken = default);

    Task<Result<CustomerProfileDto>> ToggleTwoFactorAsync(
        Toggle2FARequest request,
        CancellationToken cancellationToken = default);
}
