using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Content;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminBanners;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminBannerIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task AdminBannerEndpoints_WithBearerToken_CreateListAndUpdateBanner()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/banners",
            new
            {
                title = "Hero Banner",
                imageUrl = "https://example.test/hero.jpg",
                linkUrl = "https://example.test/desks",
                sortOrder = 1,
                isActive = true
            });
        var createJson = await createResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.True(createJson["success"]!.GetValue<bool>());
        var bannerId = createJson["data"]!["id"]!.GetValue<Guid>();
        Assert.Equal("Hero Banner", createJson["data"]!["title"]!.GetValue<string>());

        using var listResponse = await client.GetAsync("/api/admin/banners");
        var listJson = await listResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.True(listJson["success"]!.GetValue<bool>());
        Assert.Single(listJson["data"]!.AsArray());

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/banners/{bannerId}",
            new
            {
                title = "Updated Hero Banner",
                imageUrl = "https://example.test/updated.jpg",
                linkUrl = (string?)null,
                sortOrder = 5,
                isActive = false
            });
        var updateJson = await updateResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(updateJson["success"]!.GetValue<bool>());
        Assert.Equal("Updated Hero Banner", updateJson["data"]!["title"]!.GetValue<string>());
        Assert.False(updateJson["data"]!["isActive"]!.GetValue<bool>());
    }

    [Fact]
    public async Task AdminBannerEndpoints_WithoutBearerToken_ReturnUnauthorized()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/admin/banners");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBanner_MissingBanner_ReturnsNotFoundEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.PutAsJsonAsync(
            $"/api/admin/banners/{Guid.NewGuid()}",
            new
            {
                title = "Missing Banner",
                imageUrl = "https://example.test/missing.jpg",
                linkUrl = (string?)null,
                sortOrder = 1,
                isActive = true
            });
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Contains("Banner was not found.", json["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }

    [Fact]
    public async Task CreateBanner_InvalidRequest_ReturnsValidationEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.PostAsJsonAsync(
            "/api/admin/banners",
            new
            {
                title = string.Empty,
                imageUrl = string.Empty,
                sortOrder = 1,
                isActive = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(json["success"]!.GetValue<bool>());
    }
}
