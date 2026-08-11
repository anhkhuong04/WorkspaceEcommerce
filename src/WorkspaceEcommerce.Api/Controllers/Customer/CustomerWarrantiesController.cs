using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Modules.Warranties;

namespace WorkspaceEcommerce.Api.Controllers.Customer;

[ApiController]
[Authorize(Roles = AuthRoles.Customer)]
public sealed class CustomerWarrantiesController(ICustomerWarrantyService warrantyService) : ControllerBase
{
    [HttpPost("api/customer/warranties/activate")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(ApiResponse<CustomerWarrantyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateWarrantyRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return this.ToApiResponse(await warrantyService.ActivateAsync(request, cancellationToken));
    }

    [HttpGet("api/customer/warranties")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetWarranties(
        [FromQuery] CustomerWarrantyListRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return this.ToApiResponse(await warrantyService.GetWarrantiesAsync(request, cancellationToken));
    }

    [HttpGet("api/customer/warranties/{id:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetWarranty(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return this.ToApiResponse(await warrantyService.GetWarrantyAsync(id, cancellationToken));
    }
}
