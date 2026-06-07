using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Ordering;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class CheckoutController(ICheckoutService checkoutService) : ControllerBase
{
    [HttpPost("api/checkout")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await checkoutService.CheckoutAsync(request, cancellationToken);

        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }
}
