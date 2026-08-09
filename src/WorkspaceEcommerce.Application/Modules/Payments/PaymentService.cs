using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using WorkspaceEcommerce.Application.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Payments;

internal sealed class PaymentService(
    IAppDbContext dbContext,
    IVNPayPaymentService vnPayPaymentService,
    IShipmentService shipmentService,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<Result<PaymentResultDto>> HandleVNPayReturnAsync(
        VNPayCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Parameters.Count == 0)
        {
            return Result<PaymentResultDto>.Validation(["VNPay callback parameters are required."]);
        }

        var verification = vnPayPaymentService.VerifyCallback(request.Parameters);
        if (!verification.IsValid)
        {
            return Result<PaymentResultDto>.Validation(["Invalid VNPay secure hash."]);
        }

        return await ProcessVerifiedVNPayCallbackAsync(verification, cancellationToken);
    }

    public async Task<Result<VNPayIpnResponseDto>> HandleVNPayIpnAsync(
        VNPayCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Parameters.Count == 0)
        {
            return Result<VNPayIpnResponseDto>.Success(new VNPayIpnResponseDto("99", "Invalid request"));
        }

        var verification = vnPayPaymentService.VerifyCallback(request.Parameters);
        if (!verification.IsValid)
        {
            return Result<VNPayIpnResponseDto>.Success(new VNPayIpnResponseDto("97", "Invalid checksum"));
        }

        var result = await ProcessVerifiedVNPayCallbackAsync(verification, cancellationToken);

        return result.Status switch
        {
            ResultStatus.Success => Result<VNPayIpnResponseDto>.Success(new VNPayIpnResponseDto("00", "Confirm Success")),
            ResultStatus.NotFound => Result<VNPayIpnResponseDto>.Success(new VNPayIpnResponseDto("01", "Order not found")),
            ResultStatus.Conflict => Result<VNPayIpnResponseDto>.Success(new VNPayIpnResponseDto("04", result.FirstError ?? "Invalid amount")),
            ResultStatus.Validation => Result<VNPayIpnResponseDto>.Success(new VNPayIpnResponseDto("99", result.FirstError ?? "Invalid request")),
            _ => Result<VNPayIpnResponseDto>.Success(new VNPayIpnResponseDto("99", result.FirstError ?? "Unknown error"))
        };
    }

    public async Task<Result<PaymentResultDto>> GetPaymentResultAsync(
        string orderCode,
        string? phone = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedOrderCode = NormalizeOrderCode(orderCode);
        if (normalizedOrderCode is null)
        {
            return Result<PaymentResultDto>.Validation(["Order code is required."]);
        }

        var normalizedPhone = NormalizeOptional(phone);
        var orders = dbContext.Orders.Where(existing => existing.OrderCode == normalizedOrderCode);
        if (normalizedPhone is not null)
        {
            orders = orders.Where(existing => existing.CustomerPhone == normalizedPhone);
        }

        var order = await orders
            .AsNoTrackingIfEf()
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<PaymentResultDto>.NotFound("Order was not found.");
        }

        var transaction = await dbContext.PaymentTransactions
            .AsNoTrackingIfEf()
            .Where(existing => existing.OrderId == order.Id)
            .OrderByDescending(existing => existing.CreatedAt)
            .ThenByDescending(existing => existing.Id)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return Result<PaymentResultDto>.Success(ToPaymentResultDto(
            order,
            transaction,
            transaction?.GatewayResponseCode,
            transaction?.GatewayResponseMessage));
    }

    private async Task<Result<PaymentResultDto>> ProcessVerifiedVNPayCallbackAsync(
        VNPayCallbackVerificationResult verification,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var txnRef = NormalizeTxnRef(verification.TxnRef);
        if (txnRef is null)
        {
            return Result<PaymentResultDto>.Validation(["VNPay transaction reference is required."]);
        }

        var transaction = await dbContext.PaymentTransactions
            .Where(existing => existing.Provider == PaymentProvider.VNPay && existing.TxnRef == txnRef)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (transaction is null)
        {
            return Result<PaymentResultDto>.NotFound("Payment transaction was not found.");
        }

        var order = await dbContext.Orders
            .Where(existing => existing.Id == transaction.OrderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (order is null)
        {
            return Result<PaymentResultDto>.NotFound("Order was not found.");
        }

        if (verification.Amount is not null && verification.Amount.Value != transaction.Amount)
        {
            return Result<PaymentResultDto>.Conflict("VNPay amount does not match payment transaction amount.");
        }

        if (transaction.IsTerminal)
        {
            if (transaction.Status == PaymentTransactionStatus.Success &&
                !order.ShipmentId.HasValue &&
                !await dbContext.OrderShipments
                    .Where(shipment => shipment.OrderId == order.Id)
                    .AnyAsyncSafe(cancellationToken))
            {
                await TryCreateShipmentAfterPaymentAsync(order, cancellationToken);
            }

            return Result<PaymentResultDto>.Success(ToPaymentResultDto(
                order,
                transaction,
                transaction.GatewayResponseCode ?? verification.ResponseCode,
                transaction.GatewayResponseMessage ?? "Payment transaction already processed."));
        }

        var processedAt = DateTimeOffset.UtcNow;
        var outcome = vnPayPaymentService.GetPaymentOutcome(
            verification.ResponseCode,
            verification.TransactionStatus);
        var gatewayMessage = BuildGatewayMessage(verification.ResponseCode, outcome);
        var rawResponse = SerializeParameters(verification.Parameters);
        var shouldCreateShipment = false;

        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                switch (outcome)
                {
                    case VNPayPaymentOutcome.Success:
                        transaction.MarkSuccess(
                            verification.GatewayTransactionNo,
                            verification.ResponseCode,
                            gatewayMessage,
                            verification.SecureHash,
                            rawResponse,
                            processedAt);
                        order.MarkPaymentPaid(processedAt);
                        shouldCreateShipment = true;
                        break;
                    case VNPayPaymentOutcome.Cancelled:
                        transaction.MarkCancelled(
                            verification.GatewayTransactionNo,
                            verification.ResponseCode,
                            gatewayMessage,
                            verification.SecureHash,
                            rawResponse,
                            processedAt);
                        order.MarkPaymentCancelled();
                        break;
                    default:
                        transaction.MarkFailed(
                            verification.GatewayTransactionNo,
                            verification.ResponseCode,
                            gatewayMessage,
                            verification.SecureHash,
                            rawResponse,
                            processedAt);
                        order.MarkPaymentFailed();
                        break;
                }

                dbContext.Update(transaction);
                dbContext.Update(order);
                await dbContext.SaveChangesAsync(transactionCancellationToken);
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<PaymentResultDto>.Conflict(exception.Message);
        }

        if (shouldCreateShipment)
        {
            await TryCreateShipmentAfterPaymentAsync(order, cancellationToken);
        }

        return Result<PaymentResultDto>.Success(ToPaymentResultDto(
            order,
            transaction,
            verification.ResponseCode,
            gatewayMessage));
    }

    private async Task TryCreateShipmentAfterPaymentAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        if (order.ShipmentId is not null)
        {
            return;
        }

        var orderItems = await dbContext.OrderItems
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.Id)
            .ToArrayAsyncSafe(cancellationToken);
        if (orderItems.Length == 0)
        {
            logger.LogWarning(
                "Skipping shipment creation for paid order {OrderCode} because it has no order items.",
                order.OrderCode);
            return;
        }

        try
        {
            var shipmentResponse = await shipmentService.CreateShipmentAsync(new CreateShipmentRequest
            {
                ExternalOrderId = order.OrderCode,
                Receiver = new ShipmentContact
                {
                    Name = order.CustomerName,
                    Phone = order.CustomerPhone
                },
                DeliveryAddress = new ShippingAddress
                {
                    Street = string.IsNullOrWhiteSpace(order.ShippingStreet)
                        ? order.ShippingAddress
                        : order.ShippingStreet,
                    Ward = order.ShippingWard ?? string.Empty,
                    Province = order.ShippingProvince ?? string.Empty
                },
                Parcel = await AggregateParcelAsync(orderItems, cancellationToken),
                GoodsValueAmount = order.Subtotal,
                CodAmount = 0m,
                Note = order.Note
            }, order.OrderCode, cancellationToken);

            if (shipmentResponse.ShipmentId == Guid.Empty ||
                string.IsNullOrWhiteSpace(shipmentResponse.TrackingCode) ||
                !string.Equals(shipmentResponse.ExternalOrderId, order.OrderCode, StringComparison.OrdinalIgnoreCase) ||
                !ShipmentProviderContract.IsKnownStatus(shipmentResponse.Status))
            {
                throw new InvalidOperationException("MiniLogistics returned an invalid shipment mapping.");
            }

            order.UpdateShipmentInfo(shipmentResponse.TrackingCode, shipmentResponse.ShipmentId);
            var now = DateTimeOffset.UtcNow;
            var shipment = new OrderShipment(
                Guid.NewGuid(),
                order.Id,
                "MiniLogistics",
                shipmentResponse.ShipmentId,
                shipmentResponse.TrackingCode,
                shipmentResponse.Status,
                shipmentResponse.ShippingFeeAmount,
                shipmentResponse.Currency,
                now);
            dbContext.Update(order);
            dbContext.Add(shipment);
            dbContext.Add(new ShipmentTimelineEntry(
                Guid.NewGuid(),
                shipment.Id,
                shipmentResponse.Status,
                "Shipment created after payment confirmation.",
                now,
                ShipmentTimelineSource.ShipmentCreated,
                providerEventId: null,
                now));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException exception) when (ShipmentProviderFailure.IsTransient(exception))
        {
            ShipmentIntegrationMetrics.RecordCreateFailure();
            logger.LogWarning(
                exception,
                "Shipment creation failed after VNPay success for order {OrderCode}. Payment remains paid.",
                order.OrderCode);
            var pendingCommandExists = await dbContext.ShipmentCommandOutbox
                .Where(command => command.OrderId == order.Id &&
                    command.CommandType == ShipmentCommandType.Create &&
                    command.CompletedAtUtc == null)
                .AnyAsyncSafe(cancellationToken);
            if (!pendingCommandExists)
            {
                dbContext.Add(new ShipmentCommandOutbox(
                    Guid.NewGuid(),
                    order.Id,
                    ShipmentCommandType.Create,
                    reason: null,
                    DateTimeOffset.UtcNow));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShipmentIntegrationMetrics.RecordCreateFailure();
            logger.LogError(
                exception,
                "Shipment creation failed permanently after VNPay success for order {OrderCode}; use admin retry after correcting the request or provider contract",
                order.OrderCode);
        }
    }

    private async Task<ShippingParcel> AggregateParcelAsync(
        IReadOnlyCollection<OrderItem> orderItems,
        CancellationToken cancellationToken)
    {
        const decimal defaultWeightKg = 0.5m;
        const decimal defaultLengthCm = 15m;
        const decimal defaultWidthCm = 10m;
        const decimal defaultHeightCm = 8m;

        var totalWeight = 0m;
        var maxLength = 0m;
        var maxWidth = 0m;
        var totalHeight = 0m;

        var variantIds = orderItems.Select(orderItem => orderItem.ProductVariantId).Distinct().ToArray();
        var variantsById = (await dbContext.ProductVariants
            .AsNoTrackingIfEf()
            .Where(existing => variantIds.Contains(existing.Id))
            .ToArrayAsyncSafe(cancellationToken))
            .ToDictionary(variant => variant.Id);

        foreach (var orderItem in orderItems)
        {
            variantsById.TryGetValue(orderItem.ProductVariantId, out var variant);
            var weight = variant?.WeightKg ?? defaultWeightKg;
            var length = variant?.LengthCm ?? defaultLengthCm;
            var width = variant?.WidthCm ?? defaultWidthCm;
            var height = variant?.HeightCm ?? defaultHeightCm;

            totalWeight += weight * orderItem.Quantity;
            if (length > maxLength) maxLength = length;
            if (width > maxWidth) maxWidth = width;
            totalHeight += height * orderItem.Quantity;
        }

        return new ShippingParcel
        {
            WeightKg = totalWeight,
            LengthCm = maxLength,
            WidthCm = maxWidth,
            HeightCm = totalHeight
        };
    }

    private static PaymentResultDto ToPaymentResultDto(
        Order order,
        PaymentTransaction? transaction,
        string? gatewayResponseCode,
        string? message)
    {
        return new PaymentResultDto(
            order.Id,
            order.OrderCode,
            order.PaymentMethod,
            order.PaymentStatus,
            order.PaidAt,
            order.ShipmentId is not null,
            order.ShipmentId,
            order.TrackingCode,
            transaction is null ? null : ToPaymentTransactionDto(transaction),
            gatewayResponseCode,
            message);
    }

    private static PaymentTransactionDto ToPaymentTransactionDto(PaymentTransaction transaction)
    {
        return new PaymentTransactionDto(
            transaction.Id,
            transaction.Provider,
            transaction.Status,
            transaction.Amount,
            transaction.CurrencyCode,
            transaction.TxnRef,
            transaction.GatewayTransactionNo,
            transaction.GatewayResponseCode,
            transaction.GatewayResponseMessage,
            transaction.CreatedAt,
            transaction.ProcessedAt);
    }

    private static string BuildGatewayMessage(string? responseCode, VNPayPaymentOutcome outcome)
    {
        return outcome switch
        {
            VNPayPaymentOutcome.Success => "Payment completed.",
            VNPayPaymentOutcome.Cancelled => "Payment cancelled by customer.",
            _ => string.IsNullOrWhiteSpace(responseCode)
                ? "Payment failed."
                : $"Payment failed with VNPay response code {responseCode}."
        };
    }

    private static string SerializeParameters(IReadOnlyDictionary<string, string?> parameters)
    {
        var sortedParameters = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            sortedParameters[parameter.Key] = parameter.Value;
        }

        return JsonSerializer.Serialize(sortedParameters);
    }

    private static string? NormalizeTxnRef(string? value)
    {
        return NormalizeOptional(value)?.ToUpperInvariant();
    }

    private static string? NormalizeOrderCode(string? value)
    {
        return NormalizeOptional(value)?.ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
