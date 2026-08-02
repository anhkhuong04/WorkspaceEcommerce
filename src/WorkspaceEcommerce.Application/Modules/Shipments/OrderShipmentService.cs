using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

internal sealed class OrderShipmentService(
    IAppDbContext dbContext,
    IShipmentService provider,
    ICurrentCustomerContext currentCustomer,
    ILoyaltyService loyaltyService,
    TimeProvider timeProvider,
    ILogger<OrderShipmentService> logger) : IOrderShipmentService
{
    public Task<Result<ShipmentTrackingDto>> GetGuestTrackingAsync(
        string orderCode,
        string phone,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(orderCode) || string.IsNullOrWhiteSpace(phone))
        {
            return Task.FromResult(Result<ShipmentTrackingDto>.Validation(["Order code and phone are required."]));
        }

        var normalizedCode = orderCode.Trim().ToUpperInvariant();
        var normalizedPhone = phone.Trim();
        var order = dbContext.Orders.FirstOrDefault(candidate =>
            candidate.OrderCode == normalizedCode && candidate.CustomerPhone == normalizedPhone);

        return Task.FromResult(order is null
            ? Result<ShipmentTrackingDto>.NotFound("Order was not found.")
            : Result<ShipmentTrackingDto>.Success(ToDto(order)));
    }

    public Task<Result<ShipmentTrackingDto>> GetCustomerTrackingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentCustomer.CustomerId.HasValue)
        {
            return Task.FromResult(Result<ShipmentTrackingDto>.Unauthorized("Customer authentication is required."));
        }

        var customerId = currentCustomer.CustomerId.Value;
        var order = dbContext.Orders.FirstOrDefault(candidate =>
            candidate.Id == orderId && candidate.CustomerId == customerId);

        return Task.FromResult(order is null
            ? Result<ShipmentTrackingDto>.NotFound("Order was not found.")
            : Result<ShipmentTrackingDto>.Success(ToDto(order)));
    }

    public Task<Result<ShipmentTrackingDto>> GetAdminTrackingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = dbContext.Orders.FirstOrDefault(candidate => candidate.Id == orderId);
        return Task.FromResult(order is null
            ? Result<ShipmentTrackingDto>.NotFound("Order was not found.")
            : Result<ShipmentTrackingDto>.Success(ToDto(order)));
    }

    public async Task<Result<ShipmentTrackingDto>> RefreshAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = dbContext.Orders.FirstOrDefault(candidate => candidate.Id == orderId);
        if (order is null)
        {
            return Result<ShipmentTrackingDto>.NotFound("Order was not found.");
        }

        var shipment = dbContext.OrderShipments.FirstOrDefault(candidate => candidate.OrderId == orderId);
        if (shipment is null || string.IsNullOrWhiteSpace(shipment.TrackingCode))
        {
            return Result<ShipmentTrackingDto>.Conflict("Order does not have a shipment to refresh.");
        }

        try
        {
            var tracking = await provider.GetTrackingAsync(shipment.TrackingCode, cancellationToken);
            var applyResult = await ApplyTrackingAsync(order, shipment, tracking, ShipmentTimelineSource.LiveRefresh, cancellationToken);
            return applyResult.IsSuccess
                ? Result<ShipmentTrackingDto>.Success(ToDto(order))
                : ToTrackingFailure(applyResult, "Tracking response was invalid.");
        }
        catch (HttpRequestException exception)
        {
            ShipmentIntegrationMetrics.RecordTrackingRefreshFailure();
            logger.LogWarning(
                exception,
                "Tracking refresh failed for order {OrderCode}, tracking {TrackingCode}",
                order.OrderCode,
                shipment.TrackingCode);
            return Result<ShipmentTrackingDto>.Failure("Shipment provider is temporarily unavailable. Local tracking remains available.");
        }
    }

    public async Task<Result<ShipmentTrackingDto>> RetryCreateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = dbContext.Orders.FirstOrDefault(candidate => candidate.Id == orderId);
        if (order is null)
        {
            return Result<ShipmentTrackingDto>.NotFound("Order was not found.");
        }

        if (!CanCreate(order))
        {
            return Result<ShipmentTrackingDto>.Conflict("Shipment cannot be created for this order.");
        }

        var result = await TryCreateAsync(order, queueOnFailure: true, cancellationToken);
        return result.IsSuccess
            ? Result<ShipmentTrackingDto>.Success(ToDto(order))
            : ToTrackingFailure(result, "Shipment could not be created.");
    }

    public async Task<Result<ShipmentTrackingDto>> CancelAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var order = dbContext.Orders.FirstOrDefault(candidate => candidate.Id == orderId);
        if (order is null)
        {
            return Result<ShipmentTrackingDto>.NotFound("Order was not found.");
        }

        var shipment = dbContext.OrderShipments.FirstOrDefault(candidate => candidate.OrderId == orderId);
        if (shipment is null)
        {
            return Result<ShipmentTrackingDto>.Conflict("Order does not have a shipment to cancel.");
        }

        if (!CanCancel(order, shipment))
        {
            return Result<ShipmentTrackingDto>.Conflict("Shipment cannot be cancelled at its current order or provider status.");
        }

        try
        {
            var tracking = await provider.CancelShipmentAsync(
                shipment.TrackingCode,
                NormalizeCancelReason(reason),
                cancellationToken);
            var applyResult = await ApplyTrackingAsync(order, shipment, tracking, ShipmentTimelineSource.Cancellation, cancellationToken);
            return applyResult.IsSuccess
                ? Result<ShipmentTrackingDto>.Success(ToDto(order))
                : ToTrackingFailure(applyResult, "Cancellation response was invalid.");
        }
        catch (HttpRequestException exception) when (ShipmentProviderFailure.IsTransient(exception))
        {
            ShipmentIntegrationMetrics.RecordCancelFailure();
            logger.LogWarning(
                exception,
                "Shipment cancellation failed for order {OrderCode}, tracking {TrackingCode}; command will be retried",
                order.OrderCode,
                shipment.TrackingCode);
            await EnqueueCommandAsync(order.Id, ShipmentCommandType.Cancel, NormalizeCancelReason(reason), cancellationToken);
            return Result<ShipmentTrackingDto>.Success(ToDto(order));
        }
        catch (HttpRequestException exception)
        {
            ShipmentIntegrationMetrics.RecordCancelFailure();
            logger.LogWarning(
                exception,
                "Shipment cancellation was rejected for order {OrderCode}, tracking {TrackingCode}",
                order.OrderCode,
                shipment.TrackingCode);
            return Result<ShipmentTrackingDto>.Conflict("Shipment cancellation was rejected by the provider.");
        }
    }

    public Task QueueCancelAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return EnqueueCommandAsync(orderId, ShipmentCommandType.Cancel, NormalizeCancelReason(reason), cancellationToken);
    }

    public async Task<int> ProcessDueCommandsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var commandIds = dbContext.ShipmentCommandOutbox
            .Where(command => command.CompletedAtUtc == null && command.NextAttemptAtUtc <= now)
            .OrderBy(command => command.NextAttemptAtUtc)
            .ThenBy(command => command.CreatedAtUtc)
            .Take(Math.Max(1, batchSize))
            .Select(command => command.Id)
            .ToArray();
        var processed = 0;

        foreach (var commandId in commandIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = dbContext.ShipmentCommandOutbox.FirstOrDefault(candidate => candidate.Id == commandId);
            if (command is null || command.CompletedAtUtc.HasValue)
            {
                continue;
            }

            var order = dbContext.Orders.FirstOrDefault(candidate => candidate.Id == command.OrderId);
            if (order is null)
            {
                command.MarkCompleted(timeProvider.GetUtcNow());
                await dbContext.SaveChangesAsync(cancellationToken);
                processed++;
                continue;
            }

            Result operationResult;
            if (command.CommandType == ShipmentCommandType.Create)
            {
                operationResult = dbContext.OrderShipments.Any(shipment => shipment.OrderId == order.Id)
                    ? Result.Success()
                    : await TryCreateAsync(order, queueOnFailure: false, cancellationToken);
            }
            else
            {
                operationResult = await TryCancelFromOutboxAsync(order, command.Reason, cancellationToken);
            }

            if (operationResult.IsSuccess)
            {
                command.MarkCompleted(timeProvider.GetUtcNow());
            }
            else if (operationResult.Status is ResultStatus.Validation or ResultStatus.NotFound or ResultStatus.Conflict)
            {
                logger.LogWarning(
                    "Completing non-retryable shipment command {CommandId} for order {OrderCode}: {Error}",
                    command.Id,
                    order.OrderCode,
                    operationResult.FirstError);
                command.MarkCompleted(timeProvider.GetUtcNow());
            }
            else
            {
                var delayMinutes = Math.Min(60, Math.Pow(2, Math.Min(command.AttemptCount, 6)));
                command.ScheduleRetry(
                    operationResult.FirstError ?? "Shipment command failed.",
                    timeProvider.GetUtcNow().AddMinutes(delayMinutes));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            processed++;
        }

        return processed;
    }

    private async Task<Result> TryCreateAsync(
        Order order,
        bool queueOnFailure,
        CancellationToken cancellationToken)
    {
        var requestResult = BuildCreateRequest(order);
        if (requestResult.IsFailure)
        {
            return Result.Validation(requestResult.Errors);
        }

        try
        {
            var initialRequest = requestResult.Value!;
            var quote = await provider.GetShippingQuoteAsync(new ShippingQuoteRequest
            {
                ExternalOrderId = order.OrderCode,
                DeliveryAddress = initialRequest.DeliveryAddress,
                Parcel = initialRequest.Parcel,
                GoodsValueAmount = initialRequest.GoodsValueAmount,
                CodAmount = initialRequest.CodAmount
            }, cancellationToken);
            order.SetShippingFee(quote.TotalFeeAmount);

            requestResult = BuildCreateRequest(order);
            if (requestResult.IsFailure)
            {
                return Result.Validation(requestResult.Errors);
            }

            var response = await provider.CreateShipmentAsync(
                requestResult.Value!,
                order.OrderCode,
                cancellationToken);

            if (response.ShipmentId == Guid.Empty ||
                string.IsNullOrWhiteSpace(response.TrackingCode) ||
                !string.Equals(response.ExternalOrderId, order.OrderCode, StringComparison.OrdinalIgnoreCase) ||
                !ShipmentProviderContract.IsKnownStatus(response.Status))
            {
                return Result.Conflict("Shipment provider returned an invalid order mapping.");
            }

            var now = timeProvider.GetUtcNow();
            var shipment = new OrderShipment(
                Guid.NewGuid(),
                order.Id,
                ShipmentProviderContract.ProviderName,
                response.ShipmentId,
                response.TrackingCode,
                response.Status,
                response.ShippingFeeAmount,
                response.Currency,
                now);
            order.UpdateShipmentInfo(response.TrackingCode, response.ShipmentId);
            dbContext.Add(shipment);
            dbContext.Add(new ShipmentTimelineEntry(
                Guid.NewGuid(),
                shipment.Id,
                response.Status,
                "Shipment created.",
                now,
                ShipmentTimelineSource.ShipmentCreated,
                providerEventId: null,
                now));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (HttpRequestException exception) when (ShipmentProviderFailure.IsTransient(exception))
        {
            ShipmentIntegrationMetrics.RecordCreateFailure();
            logger.LogWarning(exception, "Shipment creation failed for order {OrderCode}", order.OrderCode);
            if (queueOnFailure)
            {
                await EnqueueCommandAsync(order.Id, ShipmentCommandType.Create, reason: null, cancellationToken);
            }

            return Result.Failure(queueOnFailure
                ? "Shipment provider is unavailable. Creation was queued for retry."
                : "Shipment provider is unavailable.");
        }
        catch (HttpRequestException exception)
        {
            ShipmentIntegrationMetrics.RecordCreateFailure();
            logger.LogWarning(
                exception,
                "Shipment creation was rejected for order {OrderCode} with status {StatusCode}",
                order.OrderCode,
                exception.StatusCode);
            return Result.Conflict("Shipment provider rejected the create request.");
        }
    }

    private async Task<Result> TryCancelFromOutboxAsync(
        Order order,
        string? reason,
        CancellationToken cancellationToken)
    {
        var shipment = dbContext.OrderShipments.FirstOrDefault(candidate => candidate.OrderId == order.Id);
        if (shipment is null)
        {
            return Result.Success();
        }

        try
        {
            var tracking = await provider.CancelShipmentAsync(
                shipment.TrackingCode,
                NormalizeCancelReason(reason),
                cancellationToken);
            return await ApplyTrackingAsync(order, shipment, tracking, ShipmentTimelineSource.Cancellation, cancellationToken);
        }
        catch (HttpRequestException exception) when (ShipmentProviderFailure.IsTransient(exception))
        {
            ShipmentIntegrationMetrics.RecordCancelFailure();
            logger.LogWarning(
                exception,
                "Queued cancellation failed for order {OrderCode}, tracking {TrackingCode}",
                order.OrderCode,
                shipment.TrackingCode);
            return Result.Failure("Shipment cancellation provider call failed.");
        }
        catch (HttpRequestException exception)
        {
            ShipmentIntegrationMetrics.RecordCancelFailure();
            logger.LogWarning(
                exception,
                "Queued shipment cancellation was rejected for order {OrderCode}, tracking {TrackingCode}; no retry will be attempted",
                order.OrderCode,
                shipment.TrackingCode);
            return Result.Conflict("Shipment cancellation was rejected by the provider.");
        }
    }

    private async Task<Result> ApplyTrackingAsync(
        Order order,
        OrderShipment shipment,
        TrackingResponse tracking,
        ShipmentTimelineSource source,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(tracking.TrackingCode, shipment.TrackingCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(tracking.ExternalOrderId, order.OrderCode, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Conflict("Shipment provider tracking response does not match the order.");
        }

        if (!ShipmentProviderContract.IsKnownStatus(tracking.Status) ||
            tracking.Timeline.Any(entry => !ShipmentProviderContract.IsKnownStatus(entry.Status)))
        {
            return Result.Conflict("Shipment provider tracking response contains an unsupported status.");
        }

        var now = timeProvider.GetUtcNow();
        var latestEventAt = tracking.Timeline.Length == 0
            ? now
            : tracking.Timeline.Max(entry => entry.ChangedAtUtc);
        var stateApplied = shipment.ApplyProviderState(
            tracking.Status,
            tracking.ShippingFeeAmount,
            string.IsNullOrWhiteSpace(tracking.Currency) ? shipment.Currency : tracking.Currency,
            now,
            latestEventAt);

        foreach (var item in tracking.Timeline.OrderBy(entry => entry.ChangedAtUtc))
        {
            var exists = dbContext.ShipmentTimelineEntries.Any(entry =>
                entry.OrderShipmentId == shipment.Id &&
                entry.ProviderStatus == item.Status &&
                entry.ChangedAtUtc == item.ChangedAtUtc);
            if (!exists)
            {
                dbContext.Add(new ShipmentTimelineEntry(
                    Guid.NewGuid(),
                    shipment.Id,
                    item.Status,
                    item.Note,
                    item.ChangedAtUtc,
                    source,
                    providerEventId: null,
                    now));
            }
        }

        var targetStatus = ShipmentProviderContract.MapOrderStatus(tracking.Status);
        var orderUpdated = stateApplied && targetStatus.HasValue &&
            ShipmentOrderStatusTransitioner.TryTransition(order, targetStatus.Value, tracking.Status, dbContext);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (orderUpdated && order.Status == OrderStatus.Completed)
        {
            var loyaltyResult = await loyaltyService.EarnForCompletedOrderAsync(order.Id, cancellationToken);
            if (loyaltyResult.IsFailure)
            {
                return Result.Failure(loyaltyResult.FirstError ?? "Could not award loyalty points.");
            }
        }

        return Result.Success();
    }

    private Result<CreateShipmentRequest> BuildCreateRequest(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.ShippingWard) || string.IsNullOrWhiteSpace(order.ShippingProvince))
        {
            return Result<CreateShipmentRequest>.Validation(["Structured shipping address is required to create a shipment."]);
        }

        var items = dbContext.OrderItems.Where(item => item.OrderId == order.Id).ToArray();
        if (items.Length == 0)
        {
            return Result<CreateShipmentRequest>.Validation(["Order must contain at least one item."]);
        }

        var variantIds = items.Select(item => item.ProductVariantId).ToArray();
        var variants = dbContext.ProductVariants
            .Where(variant => variantIds.Contains(variant.Id))
            .ToDictionary(variant => variant.Id);
        decimal totalWeight = 0m;
        decimal maxLength = 0m;
        decimal maxWidth = 0m;
        decimal totalHeight = 0m;

        foreach (var item in items)
        {
            variants.TryGetValue(item.ProductVariantId, out var variant);
            var weight = variant?.WeightKg ?? 0.5m;
            var length = variant?.LengthCm ?? 15m;
            var width = variant?.WidthCm ?? 10m;
            var height = variant?.HeightCm ?? 8m;
            totalWeight += weight * item.Quantity;
            maxLength = Math.Max(maxLength, length);
            maxWidth = Math.Max(maxWidth, width);
            totalHeight += height * item.Quantity;
        }

        return Result<CreateShipmentRequest>.Success(new CreateShipmentRequest
        {
            ExternalOrderId = order.OrderCode,
            Receiver = new ShipmentContact { Name = order.CustomerName, Phone = order.CustomerPhone },
            DeliveryAddress = new ShippingAddress
            {
                Street = order.ShippingStreet ?? order.ShippingAddress,
                Ward = order.ShippingWard,
                Province = order.ShippingProvince
            },
            Parcel = new ShippingParcel
            {
                WeightKg = totalWeight,
                LengthCm = maxLength,
                WidthCm = maxWidth,
                HeightCm = totalHeight
            },
            GoodsValueAmount = order.Subtotal,
            CodAmount = order.PaymentMethod == PaymentMethod.Cod ? order.TotalAmount : 0m,
            Note = order.Note
        });
    }

    private async Task EnqueueCommandAsync(
        Guid orderId,
        ShipmentCommandType commandType,
        string? reason,
        CancellationToken cancellationToken)
    {
        var exists = dbContext.ShipmentCommandOutbox.Any(command =>
            command.OrderId == orderId &&
            command.CommandType == commandType &&
            command.CompletedAtUtc == null);
        if (exists)
        {
            return;
        }

        dbContext.Add(new ShipmentCommandOutbox(
            Guid.NewGuid(),
            orderId,
            commandType,
            reason,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private ShipmentTrackingDto ToDto(Order order)
    {
        var shipment = dbContext.OrderShipments.FirstOrDefault(candidate => candidate.OrderId == order.Id);
        var timeline = shipment is null
            ? []
            : dbContext.ShipmentTimelineEntries
                .Where(entry => entry.OrderShipmentId == shipment.Id)
                .OrderBy(entry => entry.ChangedAtUtc)
                .ThenBy(entry => entry.Id)
                .Select(entry => new ShipmentTimelineEntryDto(
                    entry.Id,
                    entry.ProviderStatus,
                    entry.Note,
                    entry.ChangedAtUtc,
                    entry.Source.ToString()))
                .ToArray();
        var activeCommand = dbContext.ShipmentCommandOutbox
            .Where(command => command.OrderId == order.Id && command.CompletedAtUtc == null)
            .OrderByDescending(command => command.CreatedAtUtc)
            .FirstOrDefault();

        return new ShipmentTrackingDto(
            order.Id,
            order.OrderCode,
            order.Status,
            order.ShipmentId,
            order.TrackingCode,
            shipment?.Provider,
            shipment?.ProviderStatus,
            shipment?.ShippingFeeAmount,
            shipment?.Currency,
            shipment?.LastSyncedAtUtc,
            shipment?.LastEventAtUtc,
            CanCreate(order),
            shipment is not null,
            shipment is not null && CanCancel(order, shipment),
            activeCommand?.LastError,
            timeline);
    }

    private bool CanCreate(Order order)
    {
        return string.IsNullOrWhiteSpace(order.TrackingCode) &&
            !order.ShipmentId.HasValue &&
            !dbContext.OrderShipments.Any(shipment => shipment.OrderId == order.Id) &&
            order.Status is not (OrderStatus.Cancelled or OrderStatus.Completed or OrderStatus.Returned) &&
            (order.PaymentMethod != PaymentMethod.VNPay || order.PaymentStatus == PaymentStatus.Paid);
    }

    private static bool CanCancel(Order order, OrderShipment shipment)
    {
        return order.Status is OrderStatus.Pending or OrderStatus.Confirmed &&
            shipment.ProviderStatus is "Draft" or "PendingPickup";
    }

    private static string NormalizeCancelReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "Order cancelled." : reason.Trim();
    }

    private static Result<ShipmentTrackingDto> ToTrackingFailure(Result result, string fallbackError)
    {
        var error = result.FirstError ?? fallbackError;
        return result.Status switch
        {
            ResultStatus.Validation => Result<ShipmentTrackingDto>.Validation([error]),
            ResultStatus.NotFound => Result<ShipmentTrackingDto>.NotFound(error),
            ResultStatus.Conflict => Result<ShipmentTrackingDto>.Conflict(error),
            ResultStatus.Unauthorized => Result<ShipmentTrackingDto>.Unauthorized(error),
            _ => Result<ShipmentTrackingDto>.Failure(error)
        };
    }
}
