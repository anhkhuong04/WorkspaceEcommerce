namespace WorkspaceEcommerce.Application.Abstractions.Payments;

public interface IPaymentResultAccessTokenService
{
    string Issue(Guid orderId, string orderCode);

    bool TryValidate(string? token, out PaymentResultAccessGrant grant);
}

public sealed record PaymentResultAccessGrant(
    Guid OrderId,
    string OrderCode);
