using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[Route("api/admin/dashboard")]
public sealed class DashboardController(IAdminDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await dashboardService.GetDashboardAsync(cancellationToken);

        return this.ToApiResponse(result);
    }
}
