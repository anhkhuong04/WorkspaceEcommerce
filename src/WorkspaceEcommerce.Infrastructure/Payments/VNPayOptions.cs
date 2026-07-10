namespace WorkspaceEcommerce.Infrastructure.Payments;

public sealed class VNPayOptions
{
    public const string SectionName = "Payment:VNPay";

    public string TmnCode { get; init; } = string.Empty;

    public string HashSecret { get; init; } = string.Empty;

    public string PaymentUrl { get; init; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    public string ReturnUrl { get; init; } = "http://localhost:5080/api/payments/vnpay/return";

    public string IpnUrl { get; init; } = "http://localhost:5080/api/payments/vnpay/ipn";

    public string Version { get; init; } = "2.1.0";

    public string Command { get; init; } = "pay";

    public string Locale { get; init; } = "vn";

    public string CurrCode { get; init; } = "VND";
}
