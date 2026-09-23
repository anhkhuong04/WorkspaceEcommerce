using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using WorkspaceEcommerce.Application.Abstractions.Payments;

namespace WorkspaceEcommerce.Infrastructure.Payments;

internal sealed class DataProtectionPaymentResultAccessTokenService(
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider) : IPaymentResultAccessTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
        "WorkspaceEcommerce.PaymentResultAccess.v1");

    public string Issue(Guid orderId, string orderCode)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Payment result order id is required.", nameof(orderId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(orderCode);

        var payload = new PaymentResultAccessTokenPayload(
            orderId,
            orderCode.Trim().ToUpperInvariant(),
            timeProvider.GetUtcNow().Add(TokenLifetime).ToUnixTimeSeconds());

        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    public bool TryValidate(string? token, out PaymentResultAccessGrant grant)
    {
        grant = default!;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<PaymentResultAccessTokenPayload>(
                _protector.Unprotect(token.Trim()));
            if (payload is null ||
                payload.OrderId == Guid.Empty ||
                string.IsNullOrWhiteSpace(payload.OrderCode) ||
                payload.ExpiresAtUnixTimeSeconds <= timeProvider.GetUtcNow().ToUnixTimeSeconds())
            {
                return false;
            }

            grant = new PaymentResultAccessGrant(
                payload.OrderId,
                payload.OrderCode.Trim().ToUpperInvariant());
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record PaymentResultAccessTokenPayload(
        Guid OrderId,
        string OrderCode,
        long ExpiresAtUnixTimeSeconds);
}
