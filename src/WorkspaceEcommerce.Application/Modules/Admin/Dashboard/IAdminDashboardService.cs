using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public interface IAdminDashboardService
{
    Task<Result<AdminDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default);
}
