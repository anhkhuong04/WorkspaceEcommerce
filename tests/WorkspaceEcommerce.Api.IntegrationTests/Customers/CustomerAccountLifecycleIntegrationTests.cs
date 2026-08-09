using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Customers;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CustomerAccountLifecycleIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task VerificationRequest_IsNeutralAndPersistsProtectedOutboxWorkWithoutSynchronousDelivery()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        await client.RegisterCustomerAsync();
        var beforeRequest = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.CustomerEmailOutboxMessages.CountAsync());

        using var knownResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/email-verification/request",
            new { email = "customer@example.com" });
        using var unknownResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/email-verification/request",
            new { email = "unknown@example.com" });
        var knownJson = await knownResponse.ReadJsonAsync();
        var unknownJson = await unknownResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknownResponse.StatusCode);
        Assert.Equal(knownJson["success"]!.GetValue<bool>(), unknownJson["success"]!.GetValue<bool>());
        Assert.Empty(knownJson["errors"]!.AsArray());
        Assert.Empty(unknownJson["errors"]!.AsArray());

        var state = await fixture.ExecuteDbAsync(async dbContext => new
        {
            OutboxCount = await dbContext.CustomerEmailOutboxMessages.CountAsync(),
            PendingCount = await dbContext.CustomerEmailOutboxMessages.CountAsync(message => message.SentAt == null),
            TokenHashes = await dbContext.CustomerAccountTokens.Select(token => token.TokenHash).ToArrayAsync(),
            ProtectedPayloads = await dbContext.CustomerEmailOutboxMessages.Select(message => message.ProtectedPayload).ToArrayAsync()
        });
        Assert.Equal(beforeRequest + 1, state.OutboxCount);
        Assert.Equal(state.OutboxCount, state.PendingCount);
        Assert.All(state.TokenHashes, hash => Assert.DoesNotContain("verify-email", hash, StringComparison.OrdinalIgnoreCase));
        Assert.All(state.ProtectedPayloads, payload => Assert.DoesNotContain("verify-email", payload, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RefreshAndLogout_UseHttpOnlyCookieWithoutReturningRefreshTokenInJson()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/register",
            new
            {
                fullName = "Nguyen Van A",
                phoneNumber = "0900000000",
                email = "customer@example.com",
                password = "customer-password"
            });
        var registerJson = await registerResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.True(registerResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues));
        Assert.Contains(cookieValues!, cookie => cookie.Contains("workspace_ecommerce_refresh=", StringComparison.Ordinal));
        Assert.Contains(cookieValues!, cookie => cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Null(registerJson["data"]!["refreshToken"]);

        using var refreshResponse = await client.PostAsync("/api/customer/auth/refresh", content: null);
        var refreshJson = await refreshResponse.ReadJsonAsync();
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(refreshJson["data"]!["accessToken"]!.GetValue<string>()));
        Assert.Null(refreshJson["data"]!["refreshToken"]);

        using var logoutResponse = await client.PostAsync("/api/customer/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        using var afterLogoutResponse = await client.PostAsync("/api/customer/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutResponse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefreshOfTheSameCookie_AllowsOneRotationThenRevokesTheFamily()
    {
        await fixture.ResetDatabaseAsync();
        using var registerClient = fixture.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        using var registerResponse = await registerClient.PostAsJsonAsync(
            "/api/customer/auth/register",
            new
            {
                fullName = "Nguyen Van A",
                phoneNumber = "0900000000",
                email = "customer@example.com",
                password = "customer-password"
            });
        Assert.True(registerResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues));
        var refreshCookie = cookieValues!
            .Single(cookie => cookie.StartsWith("workspace_ecommerce_refresh=", StringComparison.OrdinalIgnoreCase))
            .Split(';', 2)[0];

        using var firstClient = fixture.CreateClient();
        using var secondClient = fixture.CreateClient();
        var firstTask = SendRefreshAsync(firstClient, refreshCookie);
        var secondTask = SendRefreshAsync(secondClient, refreshCookie);
        var responses = await Task.WhenAll(firstTask, secondTask);
        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var revocationReason = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.CustomerRefreshTokenFamilies.Select(family => family.RevocationReason).SingleAsync());
        Assert.Equal("refresh_token_reuse", revocationReason);
    }

    private static Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string refreshCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/auth/refresh");
        request.Headers.Add("Cookie", refreshCookie);
        return client.SendAsync(request);
    }
}
