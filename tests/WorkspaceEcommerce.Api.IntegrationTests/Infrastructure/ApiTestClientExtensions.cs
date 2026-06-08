using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

internal static class ApiTestClientExtensions
{
    public static async Task<string> LoginAsAdminAsync(this HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new
            {
                email = "admin@example.com",
                password = "integration-test-password"
            });
        response.EnsureSuccessStatusCode();

        var json = await response.ReadJsonAsync();
        var token = json["data"]?["accessToken"]?.GetValue<string>();

        return token ?? throw new InvalidOperationException("Admin login response did not include an access token.");
    }

    public static void UseBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<JsonNode> ReadJsonAsync(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();

        return json ?? throw new InvalidOperationException("Response body was not a JSON document.");
    }
}
