using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Warranties;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
[Route("api/warranties")]
public sealed class WarrantiesController(IPublicWarrantyService warrantyService) : ControllerBase
{
    [HttpPost("lookup")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(ApiResponse<PublicWarrantyLookupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup(
        [FromBody] WarrantyLookupRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return this.ToApiResponse(await warrantyService.LookupAsync(request, cancellationToken));
    }
}
