using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize]
public sealed class OrdersController(IAdminOrderService orderService) : ControllerBase
{
    [HttpGet("api/admin/orders")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminOrderListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] AdminOrderListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await orderService.GetOrdersAsync(request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/admin/orders/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await orderService.GetOrderByIdAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPut("api/admin/orders/{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var changedBy = User.FindFirstValue(ClaimTypes.Name) ??
            User.FindFirstValue(ClaimTypes.Email) ??
            User.Identity?.Name;
        var result = await orderService.UpdateStatusAsync(id, request, changedBy, cancellationToken);

        return this.ToApiResponse(result);
    }
}
