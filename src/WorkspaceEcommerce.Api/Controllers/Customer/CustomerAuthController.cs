using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
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
        var result = await customerAuthService.LoginAsync(request, cancellationToken);

        return this.ToApiResponse(result);
    }
}
