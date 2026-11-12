using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Catalog;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CatalogIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task Catalog_SearchesLocalizedNamesAndParentDescendants_AndPairsComparePrice()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        var parent = new WorkspaceEcommerce.Domain.Modules.Catalog.Category(Guid.NewGuid(), null,
            WorkspaceEcommerce.Domain.Common.LocalizedText.Of("Furniture"), "furniture", 0, true);
        catalog.Category.MoveToParent(parent.Id);
        var premium = new WorkspaceEcommerce.Domain.Modules.Catalog.ProductVariant(Guid.NewGuid(), catalog.Product.Id,
            "PREMIUM", "Premium", null, null, 8_000_000m, 12_000_000m, 2, false, true);
        await fixture.SeedAsync(db => { db.AddRange(parent, catalog.Category, catalog.Product, catalog.Variant, premium); return Task.CompletedTask; });
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync("/api/products?search=Standing%20Desk&categorySlug=furniture&sortBy=created-desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync())["data"]!;
        var item = Assert.Single(data["items"]!.AsArray());
        Assert.Equal(3_200_000m, item!["minPrice"]!.GetValue<decimal>());
        Assert.Equal(3_900_000m, item["compareAtPrice"]!.GetValue<decimal>());
        Assert.Equal(2, item["variantCount"]!.GetValue<int>());
        using var wildcard = await client.GetAsync("/api/products?search=%25");
        Assert.Equal(0, (await wildcard.ReadJsonAsync())["data"]!["totalCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task DeleteCategory_EmptyCategory_RemovesCategory()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var createResponse = await client.PostAsJsonAsync("/api/admin/categories", new
        {
            name = new Dictionary<string, string> { ["en"] = "Temporary", ["vi"] = "Tam thoi" },
            slug = "temporary",
            sortOrder = 1,
            isActive = true
        });
        var created = await createResponse.ReadJsonAsync();
        var categoryId = created["data"]!["id"]!.GetValue<Guid>();

        using var deleteResponse = await client.DeleteAsync($"/api/admin/categories/{categoryId}");
        using var listResponse = await client.GetAsync("/api/admin/categories");
        var list = await listResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Empty(list["data"]!.AsArray());
    }

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
            name = new Dictionary<string, string> { ["en"] = "Desks", ["vi"] = "Ban lam viec" },
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
