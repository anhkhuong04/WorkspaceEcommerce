using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Abstractions.Authentication;

namespace WorkspaceEcommerce.Api.Controllers.Customer;

[ApiController]
public sealed class CustomerAuthController(
    ICustomerAuthService customerAuthService,
    ICustomerTwoFactorService twoFactorService,
    ICustomerAccountLifecycleService accountLifecycleService,
    ICustomerSessionService sessionService,
    CustomerAccountLifecycleOptions lifecycleOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "workspace_ecommerce_refresh";
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
        return ToSessionApiResponse(result, StatusCodes.Status201Created);
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
        return ToSessionApiResponse(result);
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
        return ToSessionApiResponse(result);
    }

    [HttpPost("api/customer/auth/2fa/verify")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyTwoFactor(
        [FromBody] VerifyTwoFactorLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await twoFactorService.VerifyLoginAsync(request, cancellationToken);
        return ToSessionApiResponse(result);
    }

    [HttpPost("api/customer/auth/2fa/recovery")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyTwoFactorRecovery(
        [FromBody] VerifyTwoFactorRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await twoFactorService.VerifyRecoveryAsync(request, cancellationToken);
        return ToSessionApiResponse(result);
    }

    [HttpPost("api/customer/auth/email-verification/request")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestEmailVerification(
        [FromBody] RequestEmailVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountLifecycleService.RequestEmailVerificationAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/auth/email-verification/confirm")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmEmailVerification(
        [FromBody] ConfirmEmailVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountLifecycleService.ConfirmEmailVerificationAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/auth/password/forgot")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountLifecycleService.ForgotPasswordAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/auth/password/reset")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountLifecycleService.ResetPasswordAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/auth/refresh")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await sessionService.RefreshAsync(Request.Cookies[RefreshCookieName] ?? string.Empty, cancellationToken);
        return ToSessionApiResponse(result);
    }

    [HttpPost("api/customer/auth/logout")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessionService.RevokeAsync(
            Request.Cookies[RefreshCookieName] ?? string.Empty,
            "logout",
            cancellationToken);
        DeleteRefreshCookie();
        return this.ToApiResponse(Result.Success());
    }

    [HttpPost("api/customer/auth/logout-all")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AuthRoles.Customer)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var customerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(customerId, out var parsedCustomerId))
        {
            return this.ToApiResponse(Result.Unauthorized("Customer authentication is required."));
        }

        await sessionService.RevokeAllAsync(parsedCustomerId, "logout_all", cancellationToken);
        DeleteRefreshCookie();
        return this.ToApiResponse(Result.Success());
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
        if (result.IsSuccess)
        {
            DeleteRefreshCookie();
        }
        return this.ToApiResponse(result);
    }

    private IActionResult ToSessionApiResponse(
        Result<CustomerAuthResponse> result,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value?.RefreshToken))
        {
            Response.Cookies.Append(RefreshCookieName, result.Value.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/api/customer/auth",
                MaxAge = TimeSpan.FromDays(lifecycleOptions.RefreshTokenLifetimeDays),
                IsEssential = true
            });
        }

        return this.ToApiResponse(result, successStatusCode);
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/customer/auth",
            IsEssential = true
        });
    }
}
