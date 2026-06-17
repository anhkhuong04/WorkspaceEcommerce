using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Orders;

namespace WorkspaceEcommerce.Api.Controllers.Customer;

[ApiController]
[Authorize(Roles = AuthRoles.Customer)]
public sealed class CustomerOrdersController(ICustomerOrderService customerOrderService) : ControllerBase
{
    [HttpGet("api/customer/orders")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerOrderListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] CustomerOrderListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerOrderService.GetOrdersAsync(request, cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpGet("api/customer/orders/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await customerOrderService.GetOrderByIdAsync(id, cancellationToken);

        return this.ToApiResponse(result);
    }
}
