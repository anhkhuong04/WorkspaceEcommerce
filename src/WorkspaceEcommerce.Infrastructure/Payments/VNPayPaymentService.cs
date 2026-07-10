using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Abstractions.Payments;

namespace WorkspaceEcommerce.Infrastructure.Payments;

internal sealed class VNPayPaymentService(IOptions<VNPayOptions> options) : IVNPayPaymentService
{
    private const string SecureHashParameterName = "vnp_SecureHash";
    private const string SecureHashTypeParameterName = "vnp_SecureHashType";
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public string CreatePaymentUrl(VNPayCreatePaymentUrlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuredOptions = options.Value;
        ValidateOptions(configuredOptions);

        if (string.IsNullOrWhiteSpace(request.TxnRef))
        {
            throw new ArgumentException("VNPay transaction reference is required.", nameof(request));
        }

        if (request.Amount <= 0m)
        {
            throw new ArgumentException("VNPay amount must be greater than zero.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OrderInfo))
        {
            throw new ArgumentException("VNPay order info is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.IpAddress))
        {
            throw new ArgumentException("VNPay customer IP address is required.", nameof(request));
        }

        var createdAt = request.CreatedAt == default
            ? DateTimeOffset.UtcNow
            : request.CreatedAt;
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = configuredOptions.Version,
            ["vnp_Command"] = configuredOptions.Command,
            ["vnp_TmnCode"] = configuredOptions.TmnCode,
            ["vnp_Amount"] = ToGatewayAmount(request.Amount),
            ["vnp_CreateDate"] = ToVNPayDate(createdAt),
            ["vnp_CurrCode"] = configuredOptions.CurrCode,
            ["vnp_IpAddr"] = request.IpAddress.Trim(),
            ["vnp_Locale"] = string.IsNullOrWhiteSpace(request.Locale)
                ? configuredOptions.Locale
                : request.Locale.Trim(),
            ["vnp_OrderInfo"] = request.OrderInfo.Trim(),
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = string.IsNullOrWhiteSpace(request.ReturnUrl)
                ? configuredOptions.ReturnUrl
                : request.ReturnUrl.Trim(),
            ["vnp_TxnRef"] = request.TxnRef.Trim()
        };

        var secureHash = CreateSecureHash(
            parameters.Select(parameter =>
                new KeyValuePair<string, string?>(parameter.Key, parameter.Value)),
            configuredOptions.HashSecret);
        parameters[SecureHashParameterName] = secureHash;

        return BuildUrl(configuredOptions.PaymentUrl, parameters);
    }

    public VNPayCallbackVerificationResult VerifyCallback(
        IReadOnlyDictionary<string, string?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var configuredOptions = options.Value;
        ValidateOptions(configuredOptions);

        parameters.TryGetValue(SecureHashParameterName, out var receivedSecureHash);
        var isValid = !string.IsNullOrWhiteSpace(receivedSecureHash) &&
            string.Equals(
                CreateSecureHash(parameters, configuredOptions.HashSecret),
                receivedSecureHash.Trim(),
                StringComparison.OrdinalIgnoreCase);

        var amount = TryParseGatewayAmount(GetValue(parameters, "vnp_Amount"));

        return new VNPayCallbackVerificationResult(
            isValid,
            GetValue(parameters, "vnp_TxnRef"),
            amount,
            GetValue(parameters, "vnp_ResponseCode"),
            GetValue(parameters, "vnp_TransactionStatus"),
            GetValue(parameters, "vnp_TransactionNo"),
            receivedSecureHash,
            GetValue(parameters, "vnp_OrderInfo"),
            parameters.ToDictionary(
                parameter => parameter.Key,
                parameter => parameter.Value,
                StringComparer.Ordinal));
    }

    public VNPayPaymentOutcome GetPaymentOutcome(string? responseCode, string? transactionStatus)
    {
        if (string.Equals(responseCode, "00", StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(transactionStatus) ||
             string.Equals(transactionStatus, "00", StringComparison.Ordinal)))
        {
            return VNPayPaymentOutcome.Success;
        }

        return string.Equals(responseCode, "24", StringComparison.Ordinal)
            ? VNPayPaymentOutcome.Cancelled
            : VNPayPaymentOutcome.Failed;
    }

    private static string CreateSecureHash(
        IEnumerable<KeyValuePair<string, string?>> parameters,
        string hashSecret)
    {
        var hashData = string.Join(
            "&",
            parameters
                .Where(parameter =>
                    !string.IsNullOrWhiteSpace(parameter.Value) &&
                    !string.Equals(parameter.Key, SecureHashParameterName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(parameter.Key, SecureHashTypeParameterName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter =>
                    $"{Encode(parameter.Key)}={Encode(parameter.Value!.Trim())}"));

        var secretBytes = Encoding.UTF8.GetBytes(hashSecret);
        var hashDataBytes = Encoding.UTF8.GetBytes(hashData);

        using var hmac = new HMACSHA512(secretBytes);
        return Convert.ToHexString(hmac.ComputeHash(hashDataBytes)).ToLowerInvariant();
    }

    private static string BuildUrl(string baseUrl, IReadOnlyDictionary<string, string> parameters)
    {
        var query = string.Join(
            "&",
            parameters.Select(parameter => $"{Encode(parameter.Key)}={Encode(parameter.Value)}"));

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return baseUrl + separator + query;
    }

    private static string ToGatewayAmount(decimal amount)
    {
        return decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture);
    }

    private static decimal? TryParseGatewayAmount(string? value)
    {
        if (!decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gatewayAmount))
        {
            return null;
        }

        return gatewayAmount / 100m;
    }

    private static string ToVNPayDate(DateTimeOffset value)
    {
        return value
            .ToUniversalTime()
            .ToOffset(VietnamOffset)
            .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> parameters, string key)
    {
        return parameters.TryGetValue(key, out var value)
            ? string.IsNullOrWhiteSpace(value) ? null : value.Trim()
            : null;
    }

    private static string Encode(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static void ValidateOptions(VNPayOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TmnCode))
        {
            throw new InvalidOperationException("VNPay TmnCode is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.HashSecret))
        {
            throw new InvalidOperationException("VNPay HashSecret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.PaymentUrl))
        {
            throw new InvalidOperationException("VNPay PaymentUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ReturnUrl))
        {
            throw new InvalidOperationException("VNPay ReturnUrl is not configured.");
        }
    }
}
