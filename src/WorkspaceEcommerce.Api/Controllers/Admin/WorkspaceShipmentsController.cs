using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController, Authorize(Roles = "Admin")]
[Route("api/admin/orders/{orderId:guid}/workspace-shipment")]
[RequestSizeLimit(8 * 1024)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
public sealed class WorkspaceShipmentsController(IWorkspaceShipmentService service) : ControllerBase
{
    [HttpGet, ProducesResponseType(typeof(ApiResponse<WorkspaceShipmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid orderId, CancellationToken ct) => this.ToApiResponse(await service.GetAsync(orderId, ct));
    [HttpPost("actions"), ProducesResponseType(typeof(ApiResponse<WorkspaceShipmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Act(Guid orderId, WorkspaceShipmentRequest request, CancellationToken ct)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        return this.ToApiResponse(await service.ActAsync(orderId, request, string.IsNullOrWhiteSpace(jti) ? "" : $"admin-session:{jti}", ct));
    }
    [HttpGet("history"), ProducesResponseType(typeof(ApiResponse<PagedResult<WorkspaceShipmentAuditDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid orderId, [FromQuery] int pageNumber = 1, CancellationToken ct = default) => this.ToApiResponse(await service.HistoryAsync(orderId, pageNumber, ct));
}
