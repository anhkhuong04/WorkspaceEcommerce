using System.Net;
using System.Text.Json.Nodes;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Contracts;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class OpenApiContractIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task DevelopmentDocument_ContainsStableStorefrontAdminAndPartnerOperations()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())?.AsObject();
        var paths = Assert.IsType<JsonObject>(document?["paths"]);

        AssertOperation(paths, "/api/categories", "get");
        AssertOperation(paths, "/api/products", "get");
        AssertOperation(paths, "/api/customer/auth/login", "post");
        AssertOperation(paths, "/api/customer/orders", "get");
        AssertOperation(paths, "/api/admin/orders", "get");
        AssertOperation(paths, "/api/payments/vnpay/ipn", "get");
        AssertOperation(paths, "/api/payments/vnpay/ipn", "post");
    }

    private static void AssertOperation(JsonObject paths, string path, string method)
    {
        var pathItem = Assert.IsType<JsonObject>(paths[path]);
        var operation = Assert.IsType<JsonObject>(pathItem[method]);
        var responses = Assert.IsType<JsonObject>(operation["responses"]);

        Assert.Contains(responses, response => response.Key.StartsWith('2'));
    }
}
