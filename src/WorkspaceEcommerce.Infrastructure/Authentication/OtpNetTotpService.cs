using OtpNet;
using WorkspaceEcommerce.Application.Abstractions.Authentication;

namespace WorkspaceEcommerce.Infrastructure.Authentication;

internal sealed class OtpNetTotpService : ITotpService
{
    private const int SecretByteLength = 20;
    private const int StepSeconds = 30;
    private const int CodeDigits = 6;

    public string GenerateSecret()
    {
        return Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretByteLength));
    }

    public string CreateProvisioningUri(string secret, string issuer, string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        return new OtpUri(OtpType.Totp, secret, accountName.Trim(), issuer.Trim()).ToString();
    }

    public bool TryVerifyCode(string secret, string code, DateTimeOffset timestamp, out long timeStep)
    {
        timeStep = 0;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var totp = new Totp(
            Base32Encoding.ToBytes(secret),
            step: StepSeconds,
            totpSize: CodeDigits);

        return totp.VerifyTotp(
            timestamp.UtcDateTime,
            code.Trim(),
            out timeStep,
            VerificationWindow.RfcSpecifiedNetworkDelay);
    }
}
