using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Api.Controllers;

[ApiController]
public sealed class PaymentsController(
    IPaymentService paymentService,
    IConfiguration configuration) : ControllerBase
{
    private const string DefaultStorefrontBaseUrl = "http://localhost:5173";

    [HttpGet("api/payments/vnpay/return")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> VNPayReturn(CancellationToken cancellationToken)
    {
        var result = await paymentService.HandleVNPayReturnAsync(
            CreateCallbackRequestFromQuery(),
            cancellationToken);

        return Redirect(BuildStorefrontPaymentResultUrl(result));
    }

    [HttpGet("api/payments/vnpay/ipn")]
    [HttpPost("api/payments/vnpay/ipn")]
    [ProducesResponseType(typeof(VNPayIpnResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> VNPayIpn(CancellationToken cancellationToken)
    {
        var result = await paymentService.HandleVNPayIpnAsync(
            await CreateCallbackRequestAsync(cancellationToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return Ok(new VNPayIpnResponseDto(
                "99",
                result.FirstError ?? "Unknown error"));
        }

        return Ok(result.Value);
    }

    [HttpGet("api/payments/result")]
    [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPaymentResult(
        [FromQuery] string orderCode,
        [FromQuery] string? phone,
        CancellationToken cancellationToken)
    {
        var result = await paymentService.GetPaymentResultAsync(
            orderCode,
            phone,
            cancellationToken);

        return this.ToApiResponse(result);
    }

    private VNPayCallbackRequest CreateCallbackRequestFromQuery()
    {
        return new VNPayCallbackRequest
        {
            Parameters = Request.Query.ToDictionary(
                pair => pair.Key,
                pair => (string?)pair.Value.ToString(),
                StringComparer.Ordinal)
        };
    }

    private async Task<VNPayCallbackRequest> CreateCallbackRequestAsync(CancellationToken cancellationToken)
    {
        var parameters = Request.Query.ToDictionary(
            pair => pair.Key,
            pair => (string?)pair.Value.ToString(),
            StringComparer.Ordinal);

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            foreach (var pair in form)
            {
                parameters[pair.Key] = pair.Value.ToString();
            }
        }

        return new VNPayCallbackRequest { Parameters = parameters };
    }

    private string BuildStorefrontPaymentResultUrl(Result<PaymentResultDto> result)
    {
        var status = result.IsSuccess && result.Value is not null
            ? ToPaymentResultStatus(result.Value.PaymentStatus)
            : "failed";
        var query = new List<KeyValuePair<string, string?>>
        {
            new("status", status)
        };

        if (result.Value is not null)
        {
            query.Add(new KeyValuePair<string, string?>("orderCode", result.Value.OrderCode));
        }

        var baseUrl = configuration["Storefront:BaseUrl"];
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? DefaultStorefrontBaseUrl
            : baseUrl.Trim().TrimEnd('/');

        return $"{normalizedBaseUrl}/checkout/payment-result{QueryString.Create(query)}";
    }

    private static string ToPaymentResultStatus(PaymentStatus paymentStatus)
    {
        return paymentStatus switch
        {
            PaymentStatus.Paid => "success",
            PaymentStatus.Cancelled => "cancelled",
            _ => "failed"
        };
    }
}
