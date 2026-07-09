using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Infrastructure.Shipment;

namespace WorkspaceEcommerce.Api.Controllers.Webhooks;

[ApiController]
[Route("api/webhooks/minilogistics")]
public sealed class MiniLogisticsWebhookController(
    IAppDbContext dbContext,
    IOptions<MiniLogisticsOptions> options,
    ILogger<MiniLogisticsWebhookController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpPost]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        var request = HttpContext.Request;

        // 1. Extract headers
        if (!request.Headers.TryGetValue("X-MiniLogistics-Signature", out var signatureHeader) ||
            !request.Headers.TryGetValue("X-MiniLogistics-Timestamp", out var timestampHeader))
        {
            logger.LogWarning("Webhook missing signature or timestamp headers.");
            return BadRequest("Missing required security headers.");
        }

        // 2. Read raw body for signature verification
        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        // 3. Verify Signature
        var secret = options.Value.WebhookSecret;
        if (!VerifySignature(timestampHeader.ToString(), signatureHeader.ToString(), rawBody, secret))
        {
            logger.LogWarning("Webhook signature verification failed.");
            return Unauthorized("Invalid signature.");
        }

        // 4. Deserialize Payload
        MiniLogisticsWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MiniLogisticsWebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize webhook payload.");
            return BadRequest("Invalid JSON payload.");
        }

        if (payload is null)
        {
            return BadRequest("Payload cannot be null.");
        }

        logger.LogInformation(
            "Received MiniLogistics webhook event {Event} for order {ExternalOrderId} with status {Status}",
            payload.Event,
            payload.ExternalOrderId,
            payload.Status);

        if (payload.Event == "webhook.test")
        {
            return Ok(new { message = "Test event received successfully." });
        }

        if (payload.Event != "shipment.status_changed")
        {
            logger.LogWarning("Unsupported webhook event: {Event}", payload.Event);
            return Ok(); // Acknowledge to prevent retries for unsupported events
        }

        // 5. Update Order Status
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(o => o.OrderCode == payload.ExternalOrderId, cancellationToken);

        if (order is null)
        {
            logger.LogWarning("Order {OrderCode} not found for webhook tracking code {TrackingCode}", payload.ExternalOrderId, payload.TrackingCode);
            return NotFound("Order not found.");
        }

        var targetStatus = MapMiniLogisticsStatus(payload.Status);
        if (targetStatus is null)
        {
            logger.LogWarning("Unsupported shipment status: {Status}", payload.Status);
            return Ok(); // Acknowledge to prevent retries
        }

        try
        {
            TransitionOrder(order, targetStatus.Value, $"MiniLogistics status: {payload.Status}", dbContext);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            logger.LogError(ex, "Failed to transition order {OrderCode} to {TargetStatus}", order.OrderCode, targetStatus);
            return Conflict(ex.Message);
        }

        return Ok();
    }

    private bool VerifySignature(string timestamp, string signatureHeader, string body, string secret)
    {
        if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var payload = timestamp + "." + body;
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(secretBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var expectedSignature = "sha256=" + hashHex;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signatureHeader));
    }

    private static OrderStatus? MapMiniLogisticsStatus(string miniLogisticsStatus)
    {
        return miniLogisticsStatus switch
        {
            "PendingPickup" => OrderStatus.Confirmed,
            "InTransit" => OrderStatus.Shipping,
            "OutForDelivery" => OrderStatus.Shipping,
            "Delivered" => OrderStatus.Completed,
            "FailedDelivery" => OrderStatus.FailedDelivery,
            "Returned" => OrderStatus.FailedDelivery,
            "Cancelled" => OrderStatus.Cancelled,
            _ => null
        };
    }

    private void TransitionOrder(Order order, OrderStatus targetStatus, string? note, IAppDbContext dbContext)
    {
        if (order.Status == targetStatus)
        {
            return;
        }

        // Try to transition directly first
        try
        {
            var history = order.ChangeStatus(Guid.NewGuid(), targetStatus, note, "MiniLogistics Webhook");
            dbContext.Add(history);
            return;
        }
        catch (DomainException)
        {
            // If direct transition fails, attempt step-by-step transition
        }

        var current = order.Status;
        if (current == OrderStatus.Pending && (targetStatus == OrderStatus.Shipping || targetStatus == OrderStatus.Completed))
        {
            var history1 = order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, "Confirmed via webhook status change.", "MiniLogistics Webhook");
            dbContext.Add(history1);
            current = OrderStatus.Confirmed;
        }

        if (current == OrderStatus.Confirmed && (targetStatus == OrderStatus.Shipping || targetStatus == OrderStatus.Completed))
        {
            var history2 = order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, "Processing via webhook status change.", "MiniLogistics Webhook");
            dbContext.Add(history2);
            current = OrderStatus.Processing;
        }

        if (current == OrderStatus.Processing && (targetStatus == OrderStatus.Shipping || targetStatus == OrderStatus.Completed))
        {
            var history3 = order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, "Shipped via webhook status change.", "MiniLogistics Webhook");
            dbContext.Add(history3);
            current = OrderStatus.Shipping;
        }

        if (current == OrderStatus.Shipping && targetStatus == OrderStatus.Completed)
        {
            var history4 = order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, "Delivered via webhook.", "MiniLogistics Webhook");
            dbContext.Add(history4);
        }
    }
}

public sealed record MiniLogisticsWebhookPayload(
    Guid EventId,
    string Event,
    string TrackingCode,
    string ExternalOrderId,
    string Status,
    DateTimeOffset ChangedAtUtc);
