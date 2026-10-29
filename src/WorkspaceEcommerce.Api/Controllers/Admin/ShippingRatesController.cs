using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/shipping")]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
public sealed class ShippingRatesController(IShippingRateAdminService service) : ControllerBase
{
    [HttpGet("catalog")]
    [ProducesResponseType(typeof(ApiResponse<ShippingCatalogSummaryDto>), StatusCodes.Status200OK)]
    public IActionResult Catalog() => this.ToApiResponse(Result<ShippingCatalogSummaryDto>.Success(service.GetCatalog()));

    [HttpGet("catalog/provinces/{provinceCode}/wards")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShippingWard>>), StatusCodes.Status200OK)]
    public IActionResult Wards(string provinceCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100) =>
        this.ToApiResponse(service.GetWards(provinceCode, pageNumber, pageSize));

    [HttpGet("rate-plans")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShippingRatePlanSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] ShippingRatePlanStatus? status = null, CancellationToken cancellationToken = default) =>
        this.ToApiResponse(await service.ListAsync(pageNumber, pageSize, status, cancellationToken));

    [HttpGet("catalog/weight-issues")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShippingWeightIssueDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WeightIssues([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        this.ToApiResponse(await service.WeightIssuesAsync(pageNumber, pageSize, cancellationToken));

    [HttpGet("rate-plans/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ShippingRatePlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.GetAsync(id, cancellationToken));

    [HttpPost("rate-plans")]
    [RequestSizeLimit(256 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ShippingRatePlanDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveShippingRatePlanRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.CreateAsync(request, Actor(), cancellationToken), StatusCodes.Status201Created);

    [HttpPut("rate-plans/{id:guid}")]
    [RequestSizeLimit(256 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ShippingRatePlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveShippingRatePlanRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.UpdateAsync(id, request, Actor(), cancellationToken));

    [HttpPost("rate-plans/{id:guid}/clone")]
    [ProducesResponseType(typeof(ApiResponse<ShippingRatePlanDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Clone(Guid id, [FromBody] CloneShippingRatePlanRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.CloneAsync(id, request, Actor(), cancellationToken), StatusCodes.Status201Created);

    [HttpGet("rate-plans/{id:guid}/validation")]
    [ProducesResponseType(typeof(ApiResponse<ShippingRateCoverageResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.ValidateAsync(id, cancellationToken));

    [HttpPost("rate-plans/{id:guid}/preview")]
    [RequestSizeLimit(128 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ShippingRatePreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(Guid id, [FromBody] ShippingRatePreviewRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.PreviewAsync(id, request, cancellationToken));

    [HttpPost("rate-plans/{id:guid}/publish")]
    [ProducesResponseType(typeof(ApiResponse<ShippingRatePlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishShippingRatePlanRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.PublishAsync(id, request, Actor(), cancellationToken));

    [HttpPost("rate-plans/{id:guid}/retire")]
    [ProducesResponseType(typeof(ApiResponse<ShippingRatePlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Retire(Guid id, [FromBody] ShippingRateActionRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await service.RetireAsync(id, request, Actor(), cancellationToken));

    [HttpGet("rate-plans/{id:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShippingRateAuditDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        this.ToApiResponse(await service.HistoryAsync(id, pageNumber, pageSize, cancellationToken));

    private string Actor()
    {
        var session = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        return string.IsNullOrWhiteSpace(session) ? string.Empty : $"admin-session:{session}";
    }
}
