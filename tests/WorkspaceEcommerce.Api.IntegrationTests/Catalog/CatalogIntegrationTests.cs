using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Catalog;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CatalogIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task StorefrontCatalogEndpoints_WithSeededCatalog_ReturnVisibleCatalogData()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(
                catalog.Category,
                catalog.Product,
                catalog.Variant,
                catalog.Image,
                catalog.Specification);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();

        using var categoriesResponse = await client.GetAsync("/api/categories");
        using var productsResponse = await client.GetAsync("/api/products?categorySlug=desks&search=desk&inStock=true");
        using var detailResponse = await client.GetAsync("/api/products/standing-desk");

        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var categories = await categoriesResponse.ReadJsonAsync();
        Assert.True(categories["success"]!.GetValue<bool>());
        Assert.Equal("Desks", categories["data"]![0]!["name"]!.GetValue<string>());

        var products = await productsResponse.ReadJsonAsync();
        Assert.True(products["success"]!.GetValue<bool>());
        Assert.Equal(1, products["data"]!["totalCount"]!.GetValue<int>());
        Assert.Equal("standing-desk", products["data"]!["items"]![0]!["slug"]!.GetValue<string>());

        var detail = await detailResponse.ReadJsonAsync();
        Assert.True(detail["success"]!.GetValue<bool>());
        Assert.Equal("Standing Desk", detail["data"]!["name"]!.GetValue<string>());
        Assert.Equal("DESK-001", detail["data"]!["variants"]![0]!["sku"]!.GetValue<string>());
        Assert.Equal("https://example.test/standing-desk.jpg", detail["data"]!["images"]![0]!["imageUrl"]!.GetValue<string>());
        Assert.Equal("Material", detail["data"]!["specifications"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task StorefrontCatalogEndpoints_InactiveCategory_HideProductsAndReturnNotFoundForDetail()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        catalog.Category.Deactivate();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant);

            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();

        using var productsResponse = await client.GetAsync("/api/products");
        using var detailResponse = await client.GetAsync("/api/products/standing-desk");

        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);
        var products = await productsResponse.ReadJsonAsync();
        Assert.True(products["success"]!.GetValue<bool>());
        Assert.Equal(0, products["data"]!["totalCount"]!.GetValue<int>());

        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
        var detail = await detailResponse.ReadJsonAsync();
        Assert.False(detail["success"]!.GetValue<bool>());
        Assert.Contains(
            "Product was not found.",
            detail["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }

    [Fact]
    public async Task AdminCreateCategory_DuplicateSlug_ReturnsConflictEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());
        var request = new
        {
            name = "Desks",
            slug = "desks",
            sortOrder = 1,
            isActive = true
        };

        using var firstResponse = await client.PostAsJsonAsync("/api/admin/categories", request);
        using var duplicateResponse = await client.PostAsJsonAsync("/api/admin/categories", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var duplicate = await duplicateResponse.ReadJsonAsync();
        Assert.False(duplicate["success"]!.GetValue<bool>());
        Assert.Contains(
            "Category slug already exists.",
            duplicate["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }
}
