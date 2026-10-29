using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Modules.Operations;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

internal sealed class OrderShipmentService(
    IAppDbContext dbContext,
    IShipmentService provider,
    ICurrentCustomerContext currentCustomer,
    ILoyaltyService loyaltyService,
    TimeProvider timeProvider,
    ILogger<OrderShipmentService> logger,
    IShippingCheckoutStore? shipping = null,
    IFulfillmentCoordinator? fulfillment = null) : IOrderShipmentService
{
    private const int ShipmentCommandLeaseSeconds = 120;
    private const int MaxShipmentCommandAttempts = 8;
    private readonly string workerIdentity = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public async Task<Result<ShipmentTrackingDto>> GetGuestTrackingAsync(
        string orderCode,
        string phone,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(orderCode) || string.IsNullOrWhiteSpace(phone))
        {
            return Result<ShipmentTrackingDto>.Validation(["Order code and phone are required."]);
        }

        var normalizedCode = orderCode.Trim().ToUpperInvariant();
        var normalizedPhone = phone.Trim();
        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.OrderCode == normalizedCode && candidate.CustomerPhone == normalizedPhone)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return order is null
            ? Result<ShipmentTrackingDto>.NotFound("Order was not found.")
            : Result<ShipmentTrackingDto>.Success(await ToDtoAsync(order, cancellationToken));
    }

    public async Task<Result<ShipmentTrackingDto>> GetCustomerTrackingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentCustomer.CustomerId.HasValue)
        {
            return Result<ShipmentTrackingDto>.Unauthorized("Customer authentication is required.");
        }

        var customerId = currentCustomer.CustomerId.Value;
        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == orderId && candidate.CustomerId == customerId)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return order is null
            ? Result<ShipmentTrackingDto>.NotFound("Order was not found.")
            : Result<ShipmentTrackingDto>.Success(await ToDtoAsync(order, cancellationToken));
    }

    public async Task<Result<ShipmentTrackingDto>> GetAdminTrackingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == orderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        return order is null
            ? Result<ShipmentTrackingDto>.NotFound("Order was not found.")
            : Result<ShipmentTrackingDto>.Success(await ToDtoAsync(order, cancellationToken));
    }

    public async Task<Result<ShipmentTrackingDto>> RefreshAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Where(candidate => candidate.Id == orderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<ShipmentTrackingDto>.NotFound("Order was not found.");
        }

        var shipment = await dbContext.OrderShipments
            .Where(candidate => candidate.OrderId == orderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (shipment is null || string.IsNullOrWhiteSpace(shipment.TrackingCode))
        {
            return Result<ShipmentTrackingDto>.Conflict("Order does not have a shipment to refresh.");
        }

        if (shipment.Provider != ShipmentProviderContract.ProviderName)
            return Result<ShipmentTrackingDto>.Success(await ToDtoAsync(order, cancellationToken));
        try
        {
            var tracking = await provider.GetTrackingAsync(shipment.TrackingCode, cancellationToken);
            var applyResult = await ApplyTrackingAsync(order, shipment, tracking, ShipmentTimelineSource.LiveRefresh, cancellationToken);
            return applyResult.IsSuccess
                ? Result<ShipmentTrackingDto>.Success(await ToDtoAsync(order, cancellationToken))
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
        var order = await dbContext.Orders
            .Where(candidate => candidate.Id == orderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<ShipmentTrackingDto>.NotFound("Order was not found.");
        }

        if (!await CanCreateAsync(order, cancellationToken))
        {
            return Result<ShipmentTrackingDto>.Conflict("Shipment cannot be created for this order.");
        }

        await EnqueueCommandAsync(order.Id, ShipmentCommandType.Create, reason: null, cancellationToken);
        return Result<ShipmentTrackingDto>.Success(await ToDtoAsync(order, cancellationToken));
    }

    public async Task<Result<ShipmentTrackingDto>> CancelAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Where(candidate => candidate.Id == orderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<ShipmentTrackingDto>.NotFound("Order was not found.");
        }

        var shipment = await dbContext.OrderShipments
            .Where(candidate => candidate.OrderId == orderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (shipment is null)
        {
            return Result<ShipmentTrackingDto>.Conflict("Order does not have a shipment to cancel.");
        }

        if (shipment.Provider != ShipmentProviderContract.ProviderName)
            return Result<ShipmentTrackingDto>.Conflict("Cancel WorkspaceEC delivery through the order cancellation workflow.");

        if (!CanCancel(order, shipment))
        {
            return Result<ShipmentTrackingDto>.Conflict("Shipment cannot be cancelled at its current order or provider status.");
        }

        await EnqueueCommandAsync(order.Id, ShipmentCommandType.Cancel, NormalizeCancelReason(reason), cancellationToken);
        return Result<ShipmentTrackingDto>.Success(await ToDtoAsync(order, cancellationToken));
    }

    public async Task QueueCancelAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (fulfillment is not null && await fulfillment.MethodAsync(orderId, cancellationToken) == ShippingMethod.WorkspaceEC)
        {
            await dbContext.ExecuteInTransactionAsync(async ct =>
            {
                var order = await dbContext.FindOrderForUpdateAsync(orderId, ct);
                if (order is not null && order.Status == OrderStatus.Cancelled) await fulfillment.CancelAsync(order, NormalizeCancelReason(reason), ct);
                await dbContext.SaveChangesAsync(ct);
            }, cancellationToken);
            return;
        }
        await EnqueueCommandAsync(orderId, ShipmentCommandType.Cancel, NormalizeCancelReason(reason), cancellationToken);
    }

    public async Task<int> ProcessDueCommandsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var commands = await dbContext.ClaimDueShipmentCommandsAsync(
            workerIdentity,
            TimeSpan.FromSeconds(ShipmentCommandLeaseSeconds),
            Math.Clamp(batchSize, 1, 100),
            cancellationToken);
        OutboxProcessingMetrics.RecordClaim("shipment-command", commands.Length);
        var processed = 0;

        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!command.LeaseToken.HasValue)
            {
                continue;
            }

            var leaseToken = command.LeaseToken.Value;
            var resultPersisted = false;
            var startedAt = Stopwatch.GetTimestamp();

            var order = await dbContext.Orders
                .Where(candidate => candidate.Id == command.OrderId)
                .FirstOrDefaultAsyncSafe(cancellationToken);
            if (order is null)
            {
                resultPersisted = await dbContext.CompleteShipmentCommandLeaseAsync(
                        command.Id,
                        leaseToken,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                if (resultPersisted)
                {
                    processed++;
                    OutboxProcessingMetrics.RecordCompleted("shipment-command");
                }
                OutboxProcessingMetrics.RecordProcessingDuration(
                    "shipment-command",
                    Stopwatch.GetElapsedTime(startedAt));
                continue;
            }

            Result operationResult;
            if (fulfillment is not null && await fulfillment.MethodAsync(order.Id, cancellationToken) == ShippingMethod.WorkspaceEC)
            {
                // Local work never enters the provider pipeline, even when an old
                // or manually replayed external command references this order.
                operationResult = Result.Conflict("WorkspaceEC work cannot execute a partner command.");
            }
            else if (command.CommandType == ShipmentCommandType.Create)
            {
                operationResult = await dbContext.OrderShipments
                    .Where(shipment => shipment.OrderId == order.Id)
                    .AnyAsyncSafe(cancellationToken)
                    ? Result.Success()
                    : await TryCreateAsync(order, queueOnFailure: false, cancellationToken);
            }
            else
            {
                operationResult = await TryCancelFromOutboxAsync(order, command.Reason, cancellationToken);
            }

            if (operationResult.IsSuccess)
            {
                resultPersisted = await dbContext.CompleteShipmentCommandLeaseAsync(
                        command.Id,
                        leaseToken,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                if (resultPersisted)
                {
                    processed++;
                    OutboxProcessingMetrics.RecordCompleted("shipment-command");
                }
            }
            else if (operationResult.Status is ResultStatus.Validation or ResultStatus.NotFound or ResultStatus.Conflict)
            {
                logger.LogWarning(
                    "Dead-lettering non-retryable shipment command {CommandId} for order {OrderCode}: {Error}",
                    command.Id,
                    order.OrderCode,
                    operationResult.FirstError);
                resultPersisted = await dbContext.DeadLetterShipmentCommandLeaseAsync(
                        command.Id,
                        leaseToken,
                        operationResult.FirstError ?? "Shipment command was rejected.",
                        operationResult.Status.ToString(),
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                if (resultPersisted)
                {
                    processed++;
                    OutboxProcessingMetrics.RecordDeadLetter("shipment-command");
                }
            }
            else
            {
                var error = operationResult.FirstError ?? "Shipment command failed.";
                if (command.AttemptCount >= MaxShipmentCommandAttempts)
                {
                    resultPersisted = await dbContext.DeadLetterShipmentCommandLeaseAsync(
                            command.Id,
                            leaseToken,
                            error,
                            "TransientRetryLimitExceeded",
                            timeProvider.GetUtcNow(),
                            cancellationToken);
                    if (resultPersisted)
                    {
                        processed++;
                        OutboxProcessingMetrics.RecordDeadLetter("shipment-command");
                    }
                }
                else
                {
                    var delayMinutes = Math.Min(60, Math.Pow(2, Math.Min(command.AttemptCount, 6)));
                    var jitter = 0.9 + Random.Shared.NextDouble() * 0.2;
                    resultPersisted = await dbContext.RetryShipmentCommandLeaseAsync(
                            command.Id,
                            leaseToken,
                            error,
                            "TransientProviderFailure",
                            timeProvider.GetUtcNow().AddMinutes(delayMinutes * jitter),
                            cancellationToken);
                    if (resultPersisted)
                    {
                        processed++;
                        OutboxProcessingMetrics.RecordRetry("shipment-command");
                    }
                }
            }

            if (!resultPersisted)
            {
                logger.LogWarning(
                    "Shipment command {CommandId} lease was lost before its result could be persisted.",
                    command.Id);
            }

            OutboxProcessingMetrics.RecordProcessingDuration(
                "shipment-command",
                Stopwatch.GetElapsedTime(startedAt));
        }

        return processed;
    }

    private async Task<Result> TryCreateAsync(
        Order order,
        bool queueOnFailure,
        CancellationToken cancellationToken)
    {
        if (!await CanCreateAsync(order, cancellationToken)) return Result.Conflict("Shipment is not eligible for creation.");
        var requestResult = await BuildCreateRequestAsync(order, cancellationToken);
        if (requestResult.IsFailure)
        {
            return Result.Validation(requestResult.Errors);
        }

        try
        {
            var initialRequest = requestResult.Value!;
            var frozen = shipping is not null && await shipping.Selections.Where(x => x.OrderId == order.Id).AnyAsyncSafe(cancellationToken);
            if (!frozen)
            {
                var quote = await provider.GetShippingQuoteAsync(new ShippingQuoteRequest
                {
                    ExternalOrderId = order.OrderCode,
                    DeliveryAddress = initialRequest.DeliveryAddress,
                    Parcel = initialRequest.Parcel,
                    GoodsValueAmount = initialRequest.GoodsValueAmount,
                    CodAmount = initialRequest.CodAmount
                }, cancellationToken);
                order.SetShippingFee(quote.TotalFeeAmount);

                requestResult = await BuildCreateRequestAsync(order, cancellationToken);
                if (requestResult.IsFailure)
                {
                    return Result.Validation(requestResult.Errors);
                }
            }

            var response = await provider.CreateShipmentAsync(
                requestResult.Value!,
                order.OrderCode,
                cancellationToken);

            if (response.ShipmentId == Guid.Empty ||
                string.IsNullOrWhiteSpace(response.TrackingCode) ||
                !string.Equals(response.ExternalOrderId, order.OrderCode, StringComparison.OrdinalIgnoreCase) ||
                (frozen && response.Currency != order.CurrencyCode) ||
                !ShipmentProviderContract.IsKnownStatus(response.Status))
            {
                return Result.Conflict("Shipment provider returned an invalid order mapping.");
            }

            if (frozen)
            {
                return await shipping!.TransactionAsync(async ct =>
                {
                    var locked = await shipping.ReloadOrderForUpdateAsync(order.Id, ct);
                    if (locked is null) return Result.NotFound("Order was not found.");
                    if (await dbContext.OrderShipments.Where(x => x.OrderId == order.Id).AnyAsyncSafe(ct)) return Result.Success();
                    await PersistCreatedShipmentAsync(locked, response, ct);
                    if (locked.Status == OrderStatus.Cancelled)
                        await dbContext.TryEnqueueShipmentCommandAsync(order.Id, ShipmentCommandType.Cancel,
                            "Order cancelled while partner creation was in flight.", timeProvider.GetUtcNow(), ct);
                    return Result.Success();
                }, cancellationToken);
            }
            await PersistCreatedShipmentAsync(order, response, cancellationToken);
            return Result.Success();
        }
        catch (HttpRequestException exception) when (ShipmentProviderFailure.IsTransient(exception))
        {
            ShipmentIntegrationMetrics.RecordCreateFailure();
            logger.LogWarning(exception, "Shipment creation failed for order {OrderCode}", order.OrderCode);
            if (queueOnFailure) await EnqueueCommandAsync(order.Id, ShipmentCommandType.Create, reason: null, cancellationToken);
            return Result.Failure(queueOnFailure ? "Shipment provider is unavailable. Creation was queued for retry." : "Shipment provider is unavailable.");
        }
        catch (HttpRequestException exception)
        {
            ShipmentIntegrationMetrics.RecordCreateFailure();
            logger.LogWarning(exception, "Shipment creation was rejected for order {OrderCode} with status {StatusCode}", order.OrderCode, exception.StatusCode);
            return Result.Conflict("Shipment provider rejected the create request.");
        }
    }

    private async Task PersistCreatedShipmentAsync(Order order, CreateShipmentResponse response, CancellationToken cancellationToken)
    {
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
    }

    private async Task<Result> TryCancelFromOutboxAsync(
        Order order,
        string? reason,
        CancellationToken cancellationToken)
    {
        var shipment = await dbContext.OrderShipments
            .Where(candidate => candidate.OrderId == order.Id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (shipment is null)
        {
            return Result.Success();
        }
        if (shipment.Provider != ShipmentProviderContract.ProviderName) return Result.Conflict("Partner cancellation cannot mutate WorkspaceEC work.");

        try
        {
            var tracking = await provider.CancelShipmentAsync(
                shipment.TrackingCode,
                NormalizeCancelReason(reason),
                BuildCancelIdempotencyKey(order.OrderCode),
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
            var exists = await dbContext.ShipmentTimelineEntries
                .Where(entry => entry.OrderShipmentId == shipment.Id &&
                    entry.ProviderStatus == item.Status &&
                    entry.ChangedAtUtc == item.ChangedAtUtc)
                .AnyAsyncSafe(cancellationToken);
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
        if (fulfillment is not null) targetStatus = await fulfillment.ProviderTargetAsync(order, targetStatus, cancellationToken);
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

    private async Task<Result<CreateShipmentRequest>> BuildCreateRequestAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var selection = shipping is null ? null : await shipping.Selections.Where(x => x.OrderId == order.Id).FirstOrDefaultAsyncSafe(cancellationToken);
        if (selection is not null)
        {
            if (selection.Method != ShippingMethod.MiniLogistics) return Result<CreateShipmentRequest>.Conflict("This order does not use MiniLogistics.");
            var frozen = JsonSerializer.Deserialize<ShippingInputSnapshot>(selection.SnapshotJson);
            if (frozen is null || frozen.Version != 1) return Result<CreateShipmentRequest>.Conflict("Unsupported shipment snapshot.");
            return Result<CreateShipmentRequest>.Success(new CreateShipmentRequest
            {
                ExternalOrderId = order.OrderCode,
                Receiver = new ShipmentContact { Name = order.CustomerName, Phone = order.CustomerPhone },
                DeliveryAddress = new ShippingAddress { Street = frozen.Street, Ward = frozen.Destination.WardName, Province = frozen.Destination.ProvinceName },
                Parcel = frozen.Parcel,
                GoodsValueAmount = frozen.GoodsValue,
                CodAmount = order.PaymentMethod == PaymentMethod.Cod ? order.TotalAmount : 0,
                Note = order.Note
            });
        }
        if (string.IsNullOrWhiteSpace(order.ShippingWard) || string.IsNullOrWhiteSpace(order.ShippingProvince))
        {
            return Result<CreateShipmentRequest>.Validation(["Structured shipping address is required to create a shipment."]);
        }

        var items = await dbContext.OrderItems
            .Where(item => item.OrderId == order.Id)
            .ToArrayAsyncSafe(cancellationToken);
        if (items.Length == 0)
        {
            return Result<CreateShipmentRequest>.Validation(["Order must contain at least one item."]);
        }

        var variantIds = items.Select(item => item.ProductVariantId).ToArray();
        var variants = await dbContext.ProductVariants
            .Where(variant => variantIds.Contains(variant.Id))
            .ToDictionaryAsyncSafe(variant => variant.Id, cancellationToken);
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
        await dbContext.TryEnqueueShipmentCommandAsync(
            orderId,
            commandType,
            reason,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task<ShipmentTrackingDto> ToDtoAsync(Order order, CancellationToken cancellationToken)
    {
        var quoted = shipping is not null && await shipping.Selections.Where(x => x.OrderId == order.Id).AnyAsyncSafe(cancellationToken);
        var shipment = await dbContext.OrderShipments
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.OrderId == order.Id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        var timeline = shipment is null
            ? []
            : await dbContext.ShipmentTimelineEntries
                .AsNoTrackingIfEf()
                .Where(entry => entry.OrderShipmentId == shipment.Id)
                .OrderBy(entry => entry.ChangedAtUtc)
                .ThenBy(entry => entry.Id)
                .Select(entry => new ShipmentTimelineEntryDto(
                    entry.Id,
                    entry.ProviderStatus,
                    entry.Note,
                    entry.ChangedAtUtc,
                    entry.Source.ToString()))
                .ToArrayAsyncSafe(cancellationToken);
        var activeCommand = await dbContext.ShipmentCommandOutbox
            .AsNoTrackingIfEf()
            .Where(command => command.OrderId == order.Id &&
                (command.Status == ShipmentCommandStatus.Pending ||
                 command.Status == ShipmentCommandStatus.Leased))
            .OrderByDescending(command => command.CreatedAtUtc)
            .ThenByDescending(command => command.Id)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return new ShipmentTrackingDto(
            order.Id,
            order.OrderCode,
            order.Status,
            order.ShipmentId,
            shipment?.TrackingCode ?? order.TrackingCode,
            shipment?.Provider,
            shipment?.ProviderStatus,
            quoted ? order.ShippingFee : shipment?.ShippingFeeAmount,
            shipment?.Currency,
            shipment?.LastSyncedAtUtc,
            shipment?.LastEventAtUtc,
            await CanCreateAsync(order, cancellationToken),
            shipment is not null && shipment.Provider == ShipmentProviderContract.ProviderName,
            shipment is not null && shipment.Provider == ShipmentProviderContract.ProviderName && CanCancel(order, shipment),
            activeCommand?.LastError,
            timeline);
    }

    private async Task<bool> CanCreateAsync(Order order, CancellationToken cancellationToken)
    {
        if (fulfillment is not null)
        {
            var method = await fulfillment.MethodAsync(order.Id, cancellationToken);
            if (method == ShippingMethod.WorkspaceEC || method.HasValue && order.PaymentMethod != PaymentMethod.Cod && order.PaymentStatus != PaymentStatus.Paid) return false;
        }
        return string.IsNullOrWhiteSpace(order.TrackingCode) &&
            !order.ShipmentId.HasValue &&
            !await dbContext.OrderShipments
                .Where(shipment => shipment.OrderId == order.Id)
                .AnyAsyncSafe(cancellationToken) &&
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

    private static string BuildCancelIdempotencyKey(string orderCode) => $"{orderCode}:cancel";

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
