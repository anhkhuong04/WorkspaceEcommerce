using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Ordering.Returns;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController, Authorize(Roles = "Admin")]
[Route("api/admin/orders/{orderId:guid}/returns")]
[ProducesResponseType(typeof(ApiResponse<OrderReturnSummaryDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
public sealed class OrderReturnsController(IOrderReturnService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid orderId, [FromQuery] int pageNumber = 1, CancellationToken cancellationToken = default) =>
        this.ToApiResponse(await service.GetAsync(orderId, pageNumber, cancellationToken));
    [HttpPost]
    public async Task<IActionResult> Create(Guid orderId, [FromBody] CreateOrderReturnRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.CreateAsync(orderId, request, Actor(), cancellationToken));
    [HttpPost("reject-request")]
    public async Task<IActionResult> RejectRequest(Guid orderId, [FromBody] ReviewReturnRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.RejectRequestAsync(orderId, request, Actor(), cancellationToken));
    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid orderId, Guid id, [FromBody] ReceiveOrderReturnRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.ReceiveAsync(orderId, id, request, Actor(), cancellationToken));
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid orderId, Guid id, [FromBody] RejectOrderReturnRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.RejectAsync(orderId, id, request, Actor(), cancellationToken));
    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid orderId, Guid id, [FromBody] RefundOrderReturnRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.RefundAsync(orderId, id, request, Actor(), cancellationToken));
    private string Actor() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "";
}
