using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;

namespace WorkspaceEcommerce.Api.Controllers.Customer;

[ApiController]
public sealed class CustomerAuthController(ICustomerAuthService customerAuthService) : ControllerBase
{
    [HttpPost("api/customer/auth/register")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(
        [FromBody] CustomerRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerAuthService.RegisterAsync(request, cancellationToken);
        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPost("api/customer/auth/login")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(
        [FromBody] CustomerLoginRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
        var enrichedRequest = request with { IpAddress = ipAddress, UserAgent = userAgent };
        var result = await customerAuthService.LoginAsync(enrichedRequest, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/auth/google")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LoginWithGoogle(
        [FromBody] CustomerGoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerAuthService.LoginWithGoogleAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/auth/change-password")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = WorkspaceEcommerce.Application.Abstractions.Authentication.AuthRoles.Customer)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerAuthService.ChangePasswordAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }
}

