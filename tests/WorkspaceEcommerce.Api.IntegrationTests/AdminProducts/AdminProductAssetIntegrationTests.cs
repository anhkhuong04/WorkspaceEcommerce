using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminProducts;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminProductAssetIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task DeleteProduct_WithoutOrderHistory_RemovesProduct()
    {
        await fixture.ResetDatabaseAsync();
        var (_, product) = await SeedProductAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var deleteResponse = await client.DeleteAsync($"/api/admin/products/{product.Id}");
        using var listResponse = await client.GetAsync("/api/admin/products");
        var list = await listResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Empty(list["data"]!.AsArray());
    }

    [Fact]
    public async Task AdminProductAssetEndpoints_WithBearerToken_CreateUpdateListAndDeleteAssets()
    {
        await fixture.ResetDatabaseAsync();
        var (category, product) = await SeedProductAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var createImageResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images",
            new
            {
                imageUrl = "https://example.test/desk-1.jpg",
                altText = "Desk front",
                sortOrder = 2
            });
        var createImageJson = await createImageResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, createImageResponse.StatusCode);
        Assert.True(createImageJson["success"]!.GetValue<bool>());
        var imageId = createImageJson["data"]!["id"]!.GetValue<Guid>();
        Assert.Equal(product.Id, createImageJson["data"]!["productId"]!.GetValue<Guid>());

        using var updateImageResponse = await client.PutAsJsonAsync(
            $"/api/admin/product-images/{imageId}",
            new
            {
                imageUrl = "https://example.test/desk-updated.jpg",
                altText = "Desk updated",
                sortOrder = 1
            });
        var updateImageJson = await updateImageResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, updateImageResponse.StatusCode);
        Assert.Equal("https://example.test/desk-updated.jpg", updateImageJson["data"]!["imageUrl"]!.GetValue<string>());

        using var createSpecificationResponse = await client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/specifications",
            new
            {
                name = "Material",
                value = "Solid wood",
                sortOrder = 1
            });
        var createSpecificationJson = await createSpecificationResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, createSpecificationResponse.StatusCode);
        Assert.True(createSpecificationJson["success"]!.GetValue<bool>());
        var specificationId = createSpecificationJson["data"]!["id"]!.GetValue<Guid>();

        using var updateSpecificationResponse = await client.PutAsJsonAsync(
            $"/api/admin/product-specifications/{specificationId}",
            new
            {
                name = "Frame",
                value = "Steel",
                sortOrder = 3
            });
        var updateSpecificationJson = await updateSpecificationResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, updateSpecificationResponse.StatusCode);
        Assert.Equal("Frame", updateSpecificationJson["data"]!["name"]!.GetValue<string>());

        using var listResponse = await client.GetAsync("/api/admin/products");
        var listJson = await listResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(category.Id, listJson["data"]![0]!["categoryId"]!.GetValue<Guid>());
        Assert.Equal("https://example.test/desk-updated.jpg", listJson["data"]![0]!["images"]![0]!["imageUrl"]!.GetValue<string>());
        Assert.Equal("Frame", listJson["data"]![0]!["specifications"]![0]!["name"]!.GetValue<string>());

        using var deleteImageResponse = await client.DeleteAsync($"/api/admin/product-images/{imageId}");
        using var deleteSpecificationResponse = await client.DeleteAsync($"/api/admin/product-specifications/{specificationId}");

        Assert.Equal(HttpStatusCode.OK, deleteImageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleteSpecificationResponse.StatusCode);

        using var listAfterDeleteResponse = await client.GetAsync("/api/admin/products");
        var listAfterDeleteJson = await listAfterDeleteResponse.ReadJsonAsync();

        Assert.Empty(listAfterDeleteJson["data"]![0]!["images"]!.AsArray());
        Assert.Empty(listAfterDeleteJson["data"]![0]!["specifications"]!.AsArray());
    }

    [Fact]
    public async Task CreateProductImage_WithoutBearerToken_ReturnsUnauthorized()
    {
        await fixture.ResetDatabaseAsync();
        var (_, product) = await SeedProductAsync();
        using var client = fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images",
            new
            {
                imageUrl = "https://example.test/desk.jpg",
                sortOrder = 1
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProductSpecification_InvalidRequest_ReturnsValidationEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        var (_, product) = await SeedProductAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/specifications",
            new
            {
                name = string.Empty,
                value = string.Empty,
                sortOrder = 1
            });
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(json["success"]!.GetValue<bool>());
    }

    private async Task<(Category Category, Product Product)> SeedProductAsync()
    {
        var category = new Category(
            Guid.NewGuid(),
            null,
            LocalizedText.Of("Desks"),
            $"desks-{Guid.NewGuid():N}",
            1,
            isActive: true);
        var product = new Product(
            Guid.NewGuid(),
            category.Id,
            LocalizedText.Of("Standing Desk"),
            $"standing-desk-{Guid.NewGuid():N}",
            LocalizedText.Of("A desk for focused work."),
            isFeatured: true,
            isActive: true);

        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(category, product);

            return Task.CompletedTask;
        });

        return (category, product);
    }
}
