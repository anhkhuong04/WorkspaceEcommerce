using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class OrdersController(
    IStorefrontOrderLookupService orderLookupService,
    IOrderShipmentService shipmentService) : ControllerBase
{
    [HttpGet("api/orders/lookup")]
    [ProducesResponseType(typeof(ApiResponse<OrderLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Lookup(
        [FromQuery] OrderLookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await orderLookupService.LookupAsync(request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/orders/lookup/tracking")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupTracking(
        [FromQuery] string orderCode,
        [FromQuery] string phone,
        CancellationToken cancellationToken)
    {
        var result = await shipmentService.GetGuestTrackingAsync(orderCode, phone, cancellationToken);
        return this.ToApiResponse(result);
    }
}
