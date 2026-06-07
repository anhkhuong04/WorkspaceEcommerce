using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Admin.Authentication;

public interface IAdminAuthService
{
    Task<Result<AdminLoginResponse>> LoginAsync(
        AdminLoginRequest request,
        CancellationToken cancellationToken = default);
}
