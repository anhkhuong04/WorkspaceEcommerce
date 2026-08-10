using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Operations;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public sealed class OutboxOperationsController(IOutboxOperationsService operationsService) : ControllerBase
{
    [HttpGet("api/admin/operations/outbox")]
    [ProducesResponseType(typeof(ApiResponse<OutboxOperationsSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await operationsService.GetSummaryAsync(cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpGet("api/admin/operations/outbox/dead-letters")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<OutboxDeadLetterDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeadLetters(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await operationsService.GetDeadLettersAsync(limit, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/operations/outbox/customer-email/{id:guid}/replay")]
    [ProducesResponseType(typeof(ApiResponse<OutboxReplayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplayCustomerEmail(Guid id, CancellationToken cancellationToken)
    {
        var result = await operationsService.ReplayCustomerEmailAsync(
            id,
            GetOperatorSessionId(),
            cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/operations/outbox/shipment-command/{id:guid}/replay")]
    [ProducesResponseType(typeof(ApiResponse<OutboxReplayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplayShipmentCommand(Guid id, CancellationToken cancellationToken)
    {
        var result = await operationsService.ReplayShipmentCommandAsync(
            id,
            GetOperatorSessionId(),
            cancellationToken);
        return this.ToApiResponse(result);
    }

    private string GetOperatorSessionId() =>
        User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? string.Empty;
}
