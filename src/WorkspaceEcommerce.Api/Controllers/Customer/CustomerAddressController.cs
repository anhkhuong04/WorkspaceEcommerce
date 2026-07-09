using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;

namespace WorkspaceEcommerce.Api.Controllers.Customer;

[ApiController]
[Authorize(Roles = AuthRoles.Customer)]
public sealed class CustomerAddressController(ICustomerAddressService addressService) : ControllerBase
{
    [HttpGet("api/customer/addresses")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerAddressDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken)
    {
        var result = await addressService.GetAddressesAsync(cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("api/customer/addresses")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateAddress(
        [FromBody] SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await addressService.CreateAddressAsync(request, cancellationToken);
        return this.ToApiResponse(result, StatusCodes.Status201Created);
    }

    [HttpPut("api/customer/addresses/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateAddress(
        Guid id,
        [FromBody] SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await addressService.UpdateAddressAsync(id, request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpDelete("api/customer/addresses/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAddress(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await addressService.DeleteAddressAsync(id, cancellationToken);
        return this.ToApiResponse(result, StatusCodes.Status204NoContent);
    }

    [HttpPost("api/customer/addresses/{id:guid}/set-default")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetDefaultAddress(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await addressService.SetDefaultAddressAsync(id, cancellationToken);
        return this.ToApiResponse(result);
    }
}
