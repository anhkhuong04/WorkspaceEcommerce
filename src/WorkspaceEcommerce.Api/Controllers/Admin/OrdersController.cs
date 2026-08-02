using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public sealed class OrdersController(
    IAdminOrderService orderService,
    IOrderShipmentService shipmentService) : ControllerBase
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

    [HttpGet("api/admin/orders/{id:guid}/shipment")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShipment(Guid id, CancellationToken cancellationToken)
    {
        var result = await shipmentService.GetAdminTrackingAsync(id, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/orders/{id:guid}/shipment/refresh")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RefreshShipment(Guid id, CancellationToken cancellationToken)
    {
        var result = await shipmentService.RefreshAsync(id, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/orders/{id:guid}/shipment/retry")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryShipment(Guid id, CancellationToken cancellationToken)
    {
        var result = await shipmentService.RetryCreateAsync(id, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/orders/{id:guid}/shipment/cancel")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelShipment(
        Guid id,
        [FromBody] CancelShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await shipmentService.CancelAsync(id, request.Reason, cancellationToken);
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

    [HttpPost("api/admin/orders/import/preview")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PreviewImport(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return this.ToApiResponse(
                WorkspaceEcommerce.Application.Common.Models.Result<AdminOrderImportPreviewDto>.Validation(["Import file is required."]));
        }

        await using var stream = file.OpenReadStream();
        var result = await orderService.PreviewImportAsync(stream, file.FileName, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpPost("api/admin/orders/import/commit")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderImportCommitResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CommitImport(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return this.ToApiResponse(
                WorkspaceEcommerce.Application.Common.Models.Result<AdminOrderImportCommitResultDto>.Validation(["Import file is required."]),
                StatusCodes.Status201Created);
        }

        var changedBy = User.FindFirstValue(ClaimTypes.Name) ??
            User.FindFirstValue(ClaimTypes.Email) ??
            User.Identity?.Name;
        await using var stream = file.OpenReadStream();
        var result = await orderService.CommitImportAsync(stream, file.FileName, changedBy, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }
}

public sealed record CancelShipmentRequest(string Reason = "Order cancelled by admin.");
