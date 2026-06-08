using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Ordering;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class OrdersController(IStorefrontOrderLookupService orderLookupService) : ControllerBase
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
}
