using System.Text.Json;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Payments;

internal sealed class PaymentService(
    IAppDbContext dbContext,
    IVNPayPaymentService vnPayPaymentService) : IPaymentService
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

        Result<PaymentResultDto>? callbackResult = null;
        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                // Both callbacks can arrive at the same time (browser return and
                // VNPay IPN, or provider retries). The locked reads make one of
                // them observe the other's terminal write instead of applying the
                // payment state twice.
                var transaction = await dbContext.FindVNPayPaymentTransactionForUpdateAsync(
                    txnRef,
                    transactionCancellationToken);
                if (transaction is null)
                {
                    callbackResult = Result<PaymentResultDto>.NotFound("Payment transaction was not found.");
                    return;
                }

                var order = await dbContext.FindOrderForUpdateAsync(
                    transaction.OrderId,
                    transactionCancellationToken);
                if (order is null)
                {
                    callbackResult = Result<PaymentResultDto>.NotFound("Order was not found.");
                    return;
                }

                if (verification.Amount is not null && verification.Amount.Value != transaction.Amount)
                {
                    callbackResult = Result<PaymentResultDto>.Conflict("VNPay amount does not match payment transaction amount.");
                    return;
                }

                if (transaction.IsTerminal)
                {
                    if (transaction.Status == PaymentTransactionStatus.Success && !order.ShipmentId.HasValue)
                    {
                        await EnqueueShipmentCreateAsync(order.Id, transactionCancellationToken);
                    }

                    callbackResult = Result<PaymentResultDto>.Success(ToPaymentResultDto(
                        order,
                        transaction,
                        transaction.GatewayResponseCode ?? verification.ResponseCode,
                        transaction.GatewayResponseMessage ?? "Payment transaction already processed."));
                    return;
                }

                var processedAt = DateTimeOffset.UtcNow;
                var outcome = vnPayPaymentService.GetPaymentOutcome(
                    verification.ResponseCode,
                    verification.TransactionStatus);
                var gatewayMessage = BuildGatewayMessage(verification.ResponseCode, outcome);
                var rawResponse = SerializeParameters(verification.Parameters);

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

                if (outcome == VNPayPaymentOutcome.Success)
                {
                    // The shipment worker is the only code path that calls the
                    // provider. Keeping this insert in the same database
                    // transaction closes the paid-without-shipment crash window.
                    await EnqueueShipmentCreateAsync(order.Id, transactionCancellationToken);
                }

                callbackResult = Result<PaymentResultDto>.Success(ToPaymentResultDto(
                    order,
                    transaction,
                    verification.ResponseCode,
                    gatewayMessage));
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<PaymentResultDto>.Conflict(exception.Message);
        }

        return callbackResult ?? Result<PaymentResultDto>.Failure("Payment callback processing did not produce a result.");
    }

    private Task EnqueueShipmentCreateAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return dbContext.TryEnqueueShipmentCommandAsync(
            orderId,
            ShipmentCommandType.Create,
            reason: null,
            DateTimeOffset.UtcNow,
            cancellationToken);
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
