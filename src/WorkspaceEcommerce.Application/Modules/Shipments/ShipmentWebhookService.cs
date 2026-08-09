using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

internal sealed class ShipmentWebhookService(
    IAppDbContext dbContext,
    ILoyaltyService loyaltyService,
    TimeProvider timeProvider,
    ILogger<ShipmentWebhookService> logger) : IShipmentWebhookService
{
    public async Task<Result<ShipmentWebhookResult>> HandleAsync(
        ShipmentWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.Event) ||
            string.IsNullOrWhiteSpace(payload.TrackingCode) ||
            string.IsNullOrWhiteSpace(payload.ExternalOrderId) ||
            string.IsNullOrWhiteSpace(payload.Status) ||
            payload.ChangedAtUtc == default)
        {
            return Result<ShipmentWebhookResult>.Validation(["Shipment webhook payload is incomplete."]);
        }

        if (!ShipmentProviderContract.IsKnownStatus(payload.Status))
        {
            return Result<ShipmentWebhookResult>.Validation(["Shipment webhook contains an unsupported provider status."]);
        }

        var existingEvent = await dbContext.ShipmentEventInbox
            .Where(entry => entry.Id == payload.EventId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (existingEvent is not null)
        {
            if (!string.IsNullOrWhiteSpace(existingEvent.ProcessingError))
            {
                return Result<ShipmentWebhookResult>.Conflict(existingEvent.ProcessingError);
            }

            logger.LogInformation("Ignoring duplicate shipment webhook event {EventId}", payload.EventId);
            ShipmentIntegrationMetrics.RecordDuplicateWebhook();
            return Result<ShipmentWebhookResult>.Success(new ShipmentWebhookResult(true, false, false));
        }

        var orderCode = payload.ExternalOrderId.Trim().ToUpperInvariant();
        var trackingCode = payload.TrackingCode.Trim();
        var order = await dbContext.Orders
            .Where(candidate => candidate.OrderCode == orderCode)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<ShipmentWebhookResult>.NotFound("Order was not found for shipment webhook.");
        }

        if (!string.IsNullOrWhiteSpace(order.TrackingCode) &&
            !string.Equals(order.TrackingCode, trackingCode, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ShipmentWebhookResult>.Conflict("Shipment webhook tracking code does not match the order.");
        }

        var shipment = await dbContext.OrderShipments
            .Where(candidate => candidate.OrderId == order.Id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (shipment is not null &&
            !string.Equals(shipment.TrackingCode, trackingCode, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ShipmentWebhookResult>.Conflict("Shipment webhook tracking code does not match the persisted shipment.");
        }

        var now = timeProvider.GetUtcNow();
        var inbox = new ShipmentEventInbox(
            payload.EventId,
            payload.Event,
            trackingCode,
            orderCode,
            payload.Status,
            payload.ChangedAtUtc,
            now);

        var orderUpdated = false;
        var shipmentUpdated = false;

        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionToken =>
            {
                dbContext.Add(inbox);

                if (shipment is null)
                {
                    if (!order.ShipmentId.HasValue || order.ShipmentId.Value == Guid.Empty)
                    {
                        throw new InvalidOperationException("Order does not contain a provider shipment id.");
                    }

                    shipment = new OrderShipment(
                        Guid.NewGuid(),
                        order.Id,
                        ShipmentProviderContract.ProviderName,
                        order.ShipmentId.Value,
                        trackingCode,
                        payload.Status,
                        order.ShippingFee,
                        order.CurrencyCode,
                        payload.ChangedAtUtc);
                    dbContext.Add(shipment);
                    shipmentUpdated = true;
                }
                else
                {
                    shipmentUpdated = shipment.ApplyProviderState(
                        payload.Status,
                        shipment.ShippingFeeAmount,
                        shipment.Currency,
                        now,
                        payload.ChangedAtUtc);
                }

                var timelineExists = await dbContext.ShipmentTimelineEntries
                    .Where(entry => entry.ProviderEventId == payload.EventId ||
                        (entry.OrderShipmentId == shipment.Id &&
                         entry.ProviderStatus == payload.Status &&
                         entry.ChangedAtUtc == payload.ChangedAtUtc))
                    .AnyAsyncSafe(transactionToken);
                if (!timelineExists)
                {
                    dbContext.Add(new ShipmentTimelineEntry(
                        Guid.NewGuid(),
                        shipment.Id,
                        payload.Status,
                        $"MiniLogistics event: {payload.Event}",
                        payload.ChangedAtUtc,
                        ShipmentTimelineSource.Webhook,
                        payload.EventId,
                        now));
                }

                var targetStatus = ShipmentProviderContract.MapOrderStatus(payload.Status);
                if (shipmentUpdated && targetStatus.HasValue)
                {
                    orderUpdated = ShipmentOrderStatusTransitioner.TryTransition(
                        order,
                        targetStatus.Value,
                        payload.Status,
                        dbContext);
                }

                await dbContext.SaveChangesAsync(transactionToken);

                if (orderUpdated && order.Status == OrderStatus.Completed)
                {
                    var loyaltyResult = await loyaltyService.EarnForCompletedOrderAsync(order.Id, transactionToken);
                    if (loyaltyResult.IsFailure)
                    {
                        throw new InvalidOperationException(loyaltyResult.FirstError ?? "Could not award loyalty points.");
                    }
                }

                inbox.MarkProcessed(timeProvider.GetUtcNow());
                await dbContext.SaveChangesAsync(transactionToken);
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Shipment webhook event {EventId} failed for order {OrderCode} and tracking {TrackingCode}",
                payload.EventId,
                orderCode,
                trackingCode);
            return Result<ShipmentWebhookResult>.Failure("Shipment webhook could not be processed.");
        }

        return Result<ShipmentWebhookResult>.Success(new ShipmentWebhookResult(false, orderUpdated, shipmentUpdated));
    }
}
