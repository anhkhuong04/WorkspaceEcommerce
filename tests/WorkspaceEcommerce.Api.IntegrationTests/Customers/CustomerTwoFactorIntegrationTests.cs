using System.Net;
using System.Net.Http.Json;
using OtpNet;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Customers;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CustomerTwoFactorIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task CustomerTwoFactorEndpoints_RequireEnrollmentThenProtectLoginRecoveryAndDisableFlows()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        var initialToken = await client.RegisterCustomerAsync();
        client.UseBearerToken(initialToken);

        using var startResponse = await client.PostAsync("/api/customer/me/2fa/setup", content: null);
        var startJson = await startResponse.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var manualEntryKey = startJson["data"]!["manualEntryKey"]!.GetValue<string>();
        Assert.StartsWith("otpauth://totp/", startJson["data"]!["provisioningUri"]!.GetValue<string>(), StringComparison.Ordinal);

        var totp = new Totp(Base32Encoding.ToBytes(manualEntryKey));
        using var confirmResponse = await client.PostAsJsonAsync(
            "/api/customer/me/2fa/confirm",
            new { code = totp.ComputeTotp() });
        var confirmJson = await confirmResponse.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var recoveryCodes = confirmJson["data"]!["recoveryCodes"]!.AsArray()
            .Select(code => code!.GetValue<string>())
            .ToArray();
        Assert.Equal(10, recoveryCodes.Length);

        using var passwordLoginResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/login",
            new { email = "customer@example.com", password = "customer-password" });
        var passwordLoginJson = await passwordLoginResponse.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, passwordLoginResponse.StatusCode);
        Assert.True(passwordLoginJson["data"]!["requiresTwoFactor"]!.GetValue<bool>());
        Assert.Null(passwordLoginJson["data"]!["accessToken"]);
        var firstChallenge = passwordLoginJson["data"]!["twoFactorChallengeToken"]!.GetValue<string>();

        using var totpVerifyResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/2fa/verify",
            new { challengeToken = firstChallenge, code = totp.ComputeTotp() });
        var totpVerifyJson = await totpVerifyResponse.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, totpVerifyResponse.StatusCode);
        var totpToken = totpVerifyJson["data"]!["accessToken"]!.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(totpToken));

        using var recoveryLoginResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/login",
            new { email = "customer@example.com", password = "customer-password" });
        var recoveryLoginJson = await recoveryLoginResponse.ReadJsonAsync();
        var recoveryChallenge = recoveryLoginJson["data"]!["twoFactorChallengeToken"]!.GetValue<string>();

        using var recoveryResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/2fa/recovery",
            new { challengeToken = recoveryChallenge, recoveryCode = recoveryCodes[0] });
        var recoveryJson = await recoveryResponse.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, recoveryResponse.StatusCode);
        var recoveryToken = recoveryJson["data"]!["accessToken"]!.GetValue<string>();

        using var replayLoginResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/login",
            new { email = "customer@example.com", password = "customer-password" });
        var replayLoginJson = await replayLoginResponse.ReadJsonAsync();
        var replayChallenge = replayLoginJson["data"]!["twoFactorChallengeToken"]!.GetValue<string>();
        using var replayResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/2fa/recovery",
            new { challengeToken = replayChallenge, recoveryCode = recoveryCodes[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        client.UseBearerToken(recoveryToken);
        using var disableResponse = await client.PostAsJsonAsync(
            "/api/customer/me/2fa/disable",
            new { code = (string?)null, recoveryCode = recoveryCodes[1] });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        using var meResponse = await client.GetAsync("/api/customer/me");
        var meJson = await meResponse.ReadJsonAsync();
        Assert.False(meJson["data"]!["twoFactorEnabled"]!.GetValue<bool>());
    }
}
