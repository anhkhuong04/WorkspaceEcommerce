using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;

namespace WorkspaceEcommerce.Api.Controllers.Customer;

[ApiController]
[Authorize(Roles = AuthRoles.Customer)]
public sealed class CustomerTwoFactorController(ICustomerTwoFactorService twoFactorService) : ControllerBase
{
    [HttpPost("api/customer/me/2fa/setup")]
    [ProducesResponseType(typeof(ApiResponse<TwoFactorSetupStartResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartSetup(CancellationToken cancellationToken)
    {
        var result = await twoFactorService.StartSetupAsync(cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/me/2fa/confirm")]
    [ProducesResponseType(typeof(ApiResponse<TwoFactorSetupConfirmationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmSetup(
        [FromBody] ConfirmTwoFactorSetupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await twoFactorService.ConfirmSetupAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/me/2fa/disable")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable(
        [FromBody] DisableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await twoFactorService.DisableAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }
}
