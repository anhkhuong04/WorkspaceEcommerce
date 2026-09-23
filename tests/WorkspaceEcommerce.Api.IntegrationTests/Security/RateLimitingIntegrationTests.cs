using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Security;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class RateLimitingIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task WarrantyActivation_UsesAuthenticatedCustomerPartition()
    {
        await fixture.ResetDatabaseAsync();
        using var registrationClient = fixture.CreateClient();
        var firstToken = await registrationClient.RegisterCustomerAsync(
            email: "rate-limit-first@example.com",
            phoneNumber: "0900000011");
        var secondToken = await registrationClient.RegisterCustomerAsync(
            email: "rate-limit-second@example.com",
            phoneNumber: "0900000012");
        using var factory = fixture.CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:WarrantyActivationPermitLimit"] = "1"
        });
        using var firstCustomer = factory.CreateClient();
        using var secondCustomer = factory.CreateClient();
        firstCustomer.UseBearerToken(firstToken);
        secondCustomer.UseBearerToken(secondToken);

        using var firstResponse = await PostWarrantyActivationAsync(firstCustomer);
        using var secondCustomerResponse = await PostWarrantyActivationAsync(secondCustomer);
        using var repeatedFirstCustomerResponse = await PostWarrantyActivationAsync(firstCustomer);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, secondCustomerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, repeatedFirstCustomerResponse.StatusCode);
        AssertRetryAfter(repeatedFirstCustomerResponse);
    }

    [Fact]
    public async Task SecurityCriticalPolicies_HaveIndependentBucketsAndRetryAfter()
    {
        using var factory = fixture.CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = "1",
            ["RateLimiting:CheckoutPermitLimit"] = "1",
            ["RateLimiting:PaymentPermitLimit"] = "1",
            ["RateLimiting:ProviderWebhookPermitLimit"] = "1"
        });
        using var client = factory.CreateClient();

        using var firstCheckout = await client.PostAsJsonAsync("/api/checkout", new { });
        using var secondCheckout = await client.PostAsJsonAsync("/api/checkout", new { });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstCheckout.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondCheckout.StatusCode);
        AssertRetryAfter(secondCheckout);

        using var firstPayment = await client.GetAsync("/api/payments/result");
        using var secondPayment = await client.GetAsync("/api/payments/result");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstPayment.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondPayment.StatusCode);
        AssertRetryAfter(secondPayment);

        using var firstWebhook = await client.PostAsJsonAsync("/api/webhooks/minilogistics", new { });
        using var secondWebhook = await client.PostAsJsonAsync("/api/webhooks/minilogistics", new { });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstWebhook.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondWebhook.StatusCode);
        AssertRetryAfter(secondWebhook);

        using var firstAuth = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { email = "invalid@example.com", password = "invalid-password" });
        using var secondAuth = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { email = "invalid@example.com", password = "invalid-password" });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstAuth.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondAuth.StatusCode);
        AssertRetryAfter(secondAuth);
    }

    [Fact]
    public void PartitionKeys_UseCanonicalClientAndOpaqueCustomerIdentifiers()
    {
        var firstAnonymous = CreateHttpContext(IPAddress.Parse("192.0.2.10"));
        var secondAnonymous = CreateHttpContext(IPAddress.Parse("192.0.2.11"));
        var customerId = Guid.NewGuid();
        var authenticated = CreateHttpContext(IPAddress.Parse("192.0.2.10"));
        authenticated.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("customer_id", customerId.ToString()),
            new Claim(ClaimTypes.Email, "sensitive@example.com")
        ], "test"));

        var firstAnonymousKey = RateLimiterExtensions.GetIdentityPartitionKey(firstAnonymous);
        var secondAnonymousKey = RateLimiterExtensions.GetIdentityPartitionKey(secondAnonymous);
        var customerKey = RateLimiterExtensions.GetIdentityPartitionKey(authenticated);

        Assert.NotEqual(firstAnonymousKey, secondAnonymousKey);
        Assert.Equal("anonymous:ip:192.0.2.10", firstAnonymousKey);
        Assert.Contains(customerId.ToString("N"), customerKey, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive@example.com", customerKey, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostWarrantyActivationAsync(HttpClient client)
    {
        return client.PostAsJsonAsync(
            "/api/customer/warranties/activate",
            new { identifierType = 0, identifier = "SERIAL-NOT-FOUND" });
    }

    private static DefaultHttpContext CreateHttpContext(IPAddress remoteIpAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIpAddress;
        return context;
    }

    private static void AssertRetryAfter(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(values.Single(), out var seconds));
        Assert.True(seconds > 0);
    }
}
