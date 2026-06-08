using System.Net;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class ApiHostSmokeTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetCategories_WithMigratedPostgreSqlContainer_ReturnsOkEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", content, StringComparison.OrdinalIgnoreCase);
    }
}
