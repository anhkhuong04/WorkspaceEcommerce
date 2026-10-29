using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/orders/{id:guid}/payment")]
[RequestSizeLimit(8 * 1024)]
[ProducesResponseType(typeof(ApiResponse<OrderPaymentSummaryDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
public sealed class OrderPaymentsController(IOrderPaymentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => this.ToApiResponse(await service.GetAsync(id, ct));
    [HttpPost("confirm-transfer")]
    public Task<IActionResult> Transfer(Guid id, ConfirmOrderPaymentRequest request, CancellationToken ct) => Confirm(id, OrderPaymentReceiptKind.ManualTransfer, request, ct);
    [HttpPost("collect-cod")]
    public Task<IActionResult> Collect(Guid id, ConfirmOrderPaymentRequest request, CancellationToken ct) => Confirm(id, OrderPaymentReceiptKind.CodCollected, request, ct);
    [HttpPost("remit-cod")]
    public Task<IActionResult> Remit(Guid id, ConfirmOrderPaymentRequest request, CancellationToken ct) => Confirm(id, OrderPaymentReceiptKind.CodRemitted, request, ct);
    private async Task<IActionResult> Confirm(Guid id, OrderPaymentReceiptKind kind, ConfirmOrderPaymentRequest request, CancellationToken ct)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        return this.ToApiResponse(await service.ConfirmAsync(id, kind, request, string.IsNullOrWhiteSpace(jti) ? "" : $"admin-session:{jti}", ct));
    }
}
