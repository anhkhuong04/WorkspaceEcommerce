namespace WorkspaceEcommerce.Application.Abstractions.Payments;

public interface IVNPayPaymentService
{
    string CreatePaymentUrl(VNPayCreatePaymentUrlRequest request);

    VNPayCallbackVerificationResult VerifyCallback(
        IReadOnlyDictionary<string, string?> parameters);

    VNPayPaymentOutcome GetPaymentOutcome(string? responseCode, string? transactionStatus);
}
