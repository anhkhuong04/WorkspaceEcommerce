using System.Net;
using System.Text.Json.Nodes;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Auth;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class SignalRAuthenticationIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task CustomerBearerTokenInHubQuery_IsAcceptedOnlyForTheNotificationHub()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        var customerToken = await client.RegisterCustomerAsync();
        var encodedToken = Uri.EscapeDataString(customerToken);

        using var hubResponse = await client.PostAsync(
            $"/hubs/notifications/negotiate?negotiateVersion=1&access_token={encodedToken}",
            content: null);

        Assert.Equal(HttpStatusCode.OK, hubResponse.StatusCode);
        var negotiation = JsonNode.Parse(await hubResponse.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(negotiation?["connectionToken"]?.GetValue<string>()));

        using var ordinaryApiResponse = await client.GetAsync($"/api/customer/orders?access_token={encodedToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, ordinaryApiResponse.StatusCode);
    }
}
