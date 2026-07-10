using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class LoyaltyController(ILoyaltyService loyaltyService) : ControllerBase
{
    [HttpGet("api/loyalty/me")]
    [Authorize(Roles = AuthRoles.Customer)]
    [ProducesResponseType(typeof(ApiResponse<LoyaltyAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyLoyalty(CancellationToken cancellationToken)
    {
        var result = await loyaltyService.GetMyLoyaltyAsync(cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpGet("api/loyalty/me/transactions")]
    [Authorize(Roles = AuthRoles.Customer)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LoyaltyTransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] LoyaltyTransactionListRequest request,
        [FromQuery] int? page,
        CancellationToken cancellationToken)
    {
        var effectiveRequest = page.HasValue
            ? request with { PageNumber = page.Value }
            : request;
        var result = await loyaltyService.GetMyTransactionsAsync(effectiveRequest, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/loyalty/me/redeem")]
    [Authorize(Roles = AuthRoles.Customer)]
    [ProducesResponseType(typeof(ApiResponse<RedeemLoyaltyPointsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RedeemPoints(
        [FromBody] RedeemLoyaltyPointsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loyaltyService.RedeemPointsAsync(request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpGet("api/loyalty/tiers")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<LoyaltyTierDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTiers(CancellationToken cancellationToken)
    {
        var result = await loyaltyService.GetTiersAsync(cancellationToken);
        return this.ToApiResponse(result);
    }
}
