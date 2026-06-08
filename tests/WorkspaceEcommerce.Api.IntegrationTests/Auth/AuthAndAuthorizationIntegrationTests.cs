using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Auth;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AuthAndAuthorizationIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task Login_ValidAdminCredentials_ReturnsBearerToken()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new
            {
                email = "admin@example.com",
                password = "integration-test-password"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.True(json["success"]!.GetValue<bool>());
        Assert.Equal("Bearer", json["data"]!["tokenType"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(json["data"]!["accessToken"]!.GetValue<string>()));
    }

    [Fact]
    public async Task AdminEndpoint_WithoutToken_ReturnsUnauthorizedEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/admin/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Contains(
            "Authentication is required.",
            json["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }
}
