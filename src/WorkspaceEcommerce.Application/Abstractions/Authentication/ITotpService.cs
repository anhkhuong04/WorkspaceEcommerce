namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

public interface ITotpService
{
    string GenerateSecret();

    string CreateProvisioningUri(string secret, string issuer, string accountName);

    bool TryVerifyCode(string secret, string code, DateTimeOffset timestamp, out long timeStep);
}
