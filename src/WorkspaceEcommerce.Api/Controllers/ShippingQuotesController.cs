using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
[RequestSizeLimit(16 * 1024)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
public sealed class ShippingQuotesController(IShippingQuoteService service) : ControllerBase
{
    [HttpPost("api/checkout/shipping/options")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ShippingMethodOption>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Options(ShippingQuoteInput request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.OptionsAsync(request, cancellationToken));

    [HttpPost("api/checkout/shipping/quotes")]
    [ProducesResponseType(typeof(ApiResponse<ShippingQuoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Quote(ShippingQuoteInput request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.IssueAsync(request, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/shipping/quotes/{id:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Revoke(Guid id, RevokeShippingQuoteRequest request, CancellationToken cancellationToken)
    {
        var session = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        return this.ToApiResponse(await service.RevokeAsync(id, string.IsNullOrWhiteSpace(session) ? "" : $"admin-session:{session}", request.Reason, cancellationToken));
    }
}
