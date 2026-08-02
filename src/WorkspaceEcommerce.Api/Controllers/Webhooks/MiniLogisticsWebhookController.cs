using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Infrastructure.Shipment;

namespace WorkspaceEcommerce.Api.Controllers.Webhooks;

[ApiController]
[Route("api/webhooks/minilogistics")]
public sealed class MiniLogisticsWebhookController(
    IShipmentWebhookService webhookService,
    IOptions<MiniLogisticsOptions> options,
    TimeProvider timeProvider,
    ILogger<MiniLogisticsWebhookController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-MiniLogistics-Signature", out var signatureHeader) ||
            !Request.Headers.TryGetValue("X-MiniLogistics-Timestamp", out var timestampHeader))
        {
            ShipmentIntegrationMetrics.RecordWebhookReject();
            logger.LogWarning("MiniLogistics webhook is missing security headers");
            return BadRequest("Missing required security headers.");
        }

        var timestampText = timestampHeader.ToString();
        if (!TryValidateTimestamp(timestampText, out var timestamp))
        {
            ShipmentIntegrationMetrics.RecordWebhookReject();
            logger.LogWarning("MiniLogistics webhook timestamp is invalid or outside the allowed window");
            return Unauthorized("Invalid webhook timestamp.");
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (!VerifySignature(timestampText, signatureHeader.ToString(), rawBody, options.Value.WebhookSecret))
        {
            ShipmentIntegrationMetrics.RecordWebhookReject();
            logger.LogWarning("MiniLogistics webhook signature verification failed");
            return Unauthorized("Invalid signature.");
        }

        string? eventName;
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            eventName = document.RootElement.TryGetProperty("event", out var eventProperty)
                ? eventProperty.GetString()
                : null;
        }
        catch (JsonException)
        {
            ShipmentIntegrationMetrics.RecordWebhookReject();
            return BadRequest("Invalid JSON payload.");
        }

        if (string.Equals(eventName, ShipmentProviderContract.WebhookTestEvent, StringComparison.Ordinal))
        {
            return Ok(new { message = "Test event received successfully." });
        }

        if (eventName is not (ShipmentProviderContract.ShipmentCreatedEvent or ShipmentProviderContract.ShipmentStatusChangedEvent))
        {
            logger.LogInformation("Acknowledging unsupported MiniLogistics webhook event {Event}", eventName);
            return Ok();
        }

        if (Request.Headers.TryGetValue("X-MiniLogistics-Event", out var eventHeader) &&
            !string.Equals(eventHeader.ToString(), eventName, StringComparison.Ordinal))
        {
            ShipmentIntegrationMetrics.RecordWebhookReject();
            return BadRequest("Webhook event header does not match payload.");
        }

        ShipmentWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ShipmentWebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            ShipmentIntegrationMetrics.RecordWebhookReject();
            return BadRequest("Invalid shipment webhook payload.");
        }

        if (payload is null)
        {
            ShipmentIntegrationMetrics.RecordWebhookReject();
            return BadRequest("Webhook payload is required.");
        }

        logger.LogInformation(
            "Received MiniLogistics event {EventId} for order {OrderCode}, tracking {TrackingCode}, status {ProviderStatus}",
            payload.EventId,
            payload.ExternalOrderId,
            payload.TrackingCode,
            payload.Status);

        var result = await webhookService.HandleAsync(payload, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new { duplicate = result.Value!.IsDuplicate });
        }

        ShipmentIntegrationMetrics.RecordWebhookReject();

        return result.Status switch
        {
            ResultStatus.Validation => BadRequest(result.FirstError),
            ResultStatus.NotFound => NotFound(result.FirstError),
            ResultStatus.Conflict => Conflict(result.FirstError),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.FirstError)
        };
    }

    private bool TryValidateTimestamp(string timestampText, out DateTimeOffset timestamp)
    {
        if (!DateTimeOffset.TryParse(
                timestampText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out timestamp))
        {
            return false;
        }

        var tolerance = TimeSpan.FromSeconds(Math.Max(1, options.Value.WebhookToleranceSeconds));
        return (timeProvider.GetUtcNow() - timestamp).Duration() <= tolerance;
    }

    private static bool VerifySignature(string timestamp, string signature, string body, string secret)
    {
        if (string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        var expected = Encoding.ASCII.GetBytes($"sha256={Convert.ToHexString(hash).ToLowerInvariant()}");
        var supplied = Encoding.ASCII.GetBytes(signature.Trim());

        return expected.Length == supplied.Length &&
            CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
