using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Application.Modules.Customers.Profile;

namespace WorkspaceEcommerce.Api.Controllers.Customer;

[ApiController]
[Authorize(Roles = AuthRoles.Customer)]
public sealed class CustomerProfileController(ICustomerProfileService customerProfileService) : ControllerBase
{
    [HttpGet("api/customer/me")]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await customerProfileService.GetMeAsync(cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPut("api/customer/me")]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerProfileService.UpdateMeAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpGet("api/customer/me/stats")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAccountStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var result = await customerProfileService.GetStatsAsync(cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpGet("api/customer/me/login-history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerLoginHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLoginHistory(CancellationToken cancellationToken)
    {
        var result = await customerProfileService.GetLoginHistoryAsync(cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/me/2fa")]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ToggleTwoFactor(
        [FromBody] Toggle2FARequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerProfileService.ToggleTwoFactorAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }
}
