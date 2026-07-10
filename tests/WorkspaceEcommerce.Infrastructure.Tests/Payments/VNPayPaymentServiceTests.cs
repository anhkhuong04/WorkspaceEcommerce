using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Infrastructure.Payments;

namespace WorkspaceEcommerce.Infrastructure.Tests.Payments;

public sealed class VNPayPaymentServiceTests
{
    [Fact]
    public void CreatePaymentUrl_IncludesRequiredParametersAndSecureHash()
    {
        var service = CreateService();

        var url = service.CreatePaymentUrl(new VNPayCreatePaymentUrlRequest
        {
            TxnRef = "ORD-001",
            Amount = 125000m,
            OrderInfo = "Pay order ORD-001",
            IpAddress = "127.0.0.1",
            CreatedAt = new DateTimeOffset(2026, 7, 10, 3, 4, 5, TimeSpan.Zero)
        });
        var parameters = ParseQuery(url);

        Assert.StartsWith("https://sandbox.test/vpcpay.html?", url, StringComparison.Ordinal);
        Assert.Equal("2.1.0", parameters["vnp_Version"]);
        Assert.Equal("pay", parameters["vnp_Command"]);
        Assert.Equal("TESTTMN", parameters["vnp_TmnCode"]);
        Assert.Equal("12500000", parameters["vnp_Amount"]);
        Assert.Equal("20260710100405", parameters["vnp_CreateDate"]);
        Assert.Equal("VND", parameters["vnp_CurrCode"]);
        Assert.Equal("127.0.0.1", parameters["vnp_IpAddr"]);
        Assert.Equal("vn", parameters["vnp_Locale"]);
        Assert.Equal("Pay order ORD-001", parameters["vnp_OrderInfo"]);
        Assert.Equal("other", parameters["vnp_OrderType"]);
        Assert.Equal("https://api.test/api/payments/vnpay/return", parameters["vnp_ReturnUrl"]);
        Assert.Equal("ORD-001", parameters["vnp_TxnRef"]);
        Assert.NotNull(parameters["vnp_SecureHash"]);
        Assert.True(parameters["vnp_SecureHash"]!.Length > 0);
    }

    [Fact]
    public void VerifyCallback_ValidSignedParameters_ReturnsValidResultAndAmount()
    {
        var service = CreateService();
        var parameters = ParseQuery(service.CreatePaymentUrl(new VNPayCreatePaymentUrlRequest
        {
            TxnRef = "ORD-002",
            Amount = 10000m,
            OrderInfo = "Pay order ORD-002",
            IpAddress = "127.0.0.1",
            CreatedAt = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero)
        }));
        parameters["vnp_ResponseCode"] = "00";
        parameters["vnp_TransactionStatus"] = "00";
        parameters["vnp_TransactionNo"] = "14123456";
        Resign(parameters);

        var result = service.VerifyCallback(parameters);

        Assert.True(result.IsValid);
        Assert.Equal("ORD-002", result.TxnRef);
        Assert.Equal(10000m, result.Amount);
        Assert.Equal("00", result.ResponseCode);
        Assert.Equal("00", result.TransactionStatus);
        Assert.Equal("14123456", result.GatewayTransactionNo);
    }

    [Fact]
    public void CreatePaymentUrl_SameInput_CreatesStableSecureHash()
    {
        var service = CreateService();
        var request = new VNPayCreatePaymentUrlRequest
        {
            TxnRef = "ORD-STABLE",
            Amount = 42000m,
            OrderInfo = "Pay order ORD-STABLE",
            IpAddress = "127.0.0.1",
            CreatedAt = new DateTimeOffset(2026, 7, 10, 1, 2, 3, TimeSpan.Zero)
        };

        var first = ParseQuery(service.CreatePaymentUrl(request));
        var second = ParseQuery(service.CreatePaymentUrl(request));

        Assert.Equal(first["vnp_SecureHash"], second["vnp_SecureHash"]);
    }

    [Fact]
    public void VerifyCallback_TamperedParameters_ReturnsInvalidResult()
    {
        var service = CreateService();
        var parameters = ParseQuery(service.CreatePaymentUrl(new VNPayCreatePaymentUrlRequest
        {
            TxnRef = "ORD-003",
            Amount = 50000m,
            OrderInfo = "Pay order ORD-003",
            IpAddress = "127.0.0.1"
        }));
        parameters["vnp_Amount"] = "1";

        var result = service.VerifyCallback(parameters);

        Assert.False(result.IsValid);
        Assert.Equal(0.01m, result.Amount);
    }

    [Theory]
    [InlineData("00", "00", VNPayPaymentOutcome.Success)]
    [InlineData("00", null, VNPayPaymentOutcome.Success)]
    [InlineData("24", "02", VNPayPaymentOutcome.Cancelled)]
    [InlineData("99", "02", VNPayPaymentOutcome.Failed)]
    public void GetPaymentOutcome_MapsResponseCodes(
        string responseCode,
        string? transactionStatus,
        VNPayPaymentOutcome expectedOutcome)
    {
        var service = CreateService();

        var outcome = service.GetPaymentOutcome(responseCode, transactionStatus);

        Assert.Equal(expectedOutcome, outcome);
    }

    [Fact]
    public void CreatePaymentUrl_MissingOptions_ThrowsInvalidOperationException()
    {
        var service = new VNPayPaymentService(Options.Create(new VNPayOptions()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.CreatePaymentUrl(new VNPayCreatePaymentUrlRequest
            {
                TxnRef = "ORD-004",
                Amount = 10000m,
                OrderInfo = "Pay order ORD-004",
                IpAddress = "127.0.0.1"
            }));

        Assert.Equal("VNPay TmnCode is not configured.", exception.Message);
    }

    private static VNPayPaymentService CreateService()
    {
        return new VNPayPaymentService(Options.Create(CreateOptions()));
    }

    private static VNPayOptions CreateOptions()
    {
        return new VNPayOptions
        {
            TmnCode = "TESTTMN",
            HashSecret = "test-secret",
            PaymentUrl = "https://sandbox.test/vpcpay.html",
            ReturnUrl = "https://api.test/api/payments/vnpay/return",
            IpnUrl = "https://api.test/api/payments/vnpay/ipn",
            Version = "2.1.0",
            Command = "pay",
            Locale = "vn",
            CurrCode = "VND"
        };
    }

    private static Dictionary<string, string?> ParseQuery(string url)
    {
        var uri = new Uri(url);
        var query = uri.Query.TrimStart('?');
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : null;
            result[key] = value;
        }

        return result;
    }

    private static void Resign(Dictionary<string, string?> parameters)
    {
        var hashData = string.Join(
            "&",
            parameters
                .Where(parameter =>
                    !string.IsNullOrWhiteSpace(parameter.Value) &&
                    !string.Equals(parameter.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(parameter.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!.Trim())}"));

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(CreateOptions().HashSecret));
        parameters["vnp_SecureHash"] = Convert
            .ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(hashData)))
            .ToLowerInvariant();
    }
}
