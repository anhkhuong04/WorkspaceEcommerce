using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminProducts;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminProductAssetIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task DeleteProduct_InShoppingCart_ReturnsConflictAndPreservesProductAndCart()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        var cart = new WorkspaceEcommerce.Domain.Modules.Cart.Cart(Guid.NewGuid(), null, "product-delete-test");
        cart.AddItem(Guid.NewGuid(), catalog.Variant.Id, 2, catalog.Variant.Price);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, cart);
            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.DeleteAsync($"/api/admin/products/{catalog.Product.Id}");
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Contains("shopping cart", json["errors"]!.ToJsonString());
        Assert.Contains("Deactivate", json["errors"]!.ToJsonString());
        var state = await fixture.ExecuteDbAsync(async dbContext => new
        {
            ProductExists = await dbContext.Products.AnyAsync(product => product.Id == catalog.Product.Id),
            VariantExists = await dbContext.ProductVariants.AnyAsync(variant => variant.Id == catalog.Variant.Id),
            CartQuantity = await dbContext.CartItems.Where(item => item.CartId == cart.Id).Select(item => item.Quantity).SingleAsync()
        });
        Assert.True(state.ProductExists);
        Assert.True(state.VariantExists);
        Assert.Equal(2, state.CartQuantity);

        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Carts.Remove(cart);
            return Task.CompletedTask;
        });
        using var retryResponse = await client.DeleteAsync($"/api/admin/products/{catalog.Product.Id}");
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var deleted = await fixture.ExecuteDbAsync(async dbContext => new
        {
            ProductExists = await dbContext.Products.AnyAsync(product => product.Id == catalog.Product.Id),
            VariantExists = await dbContext.ProductVariants.AnyAsync(variant => variant.Id == catalog.Variant.Id),
            ImageExists = await dbContext.ProductImages.AnyAsync(image => image.ProductId == catalog.Product.Id),
            SpecificationExists = await dbContext.ProductSpecifications.AnyAsync(specification => specification.ProductId == catalog.Product.Id)
        });
        Assert.False(deleted.ProductExists);
        Assert.False(deleted.VariantExists);
        Assert.False(deleted.ImageExists);
        Assert.False(deleted.SpecificationExists);
    }

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
        Assert.Empty(list["data"]!["items"]!.AsArray());
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
        Assert.Equal(category.Id, listJson["data"]!["items"]![0]!["categoryId"]!.GetValue<Guid>());
        Assert.Equal("https://example.test/desk-updated.jpg", listJson["data"]!["items"]![0]!["images"]![0]!["imageUrl"]!.GetValue<string>());
        Assert.Equal("Frame", listJson["data"]!["items"]![0]!["specifications"]![0]!["name"]!.GetValue<string>());

        using var deleteImageResponse = await client.DeleteAsync($"/api/admin/product-images/{imageId}");
        using var deleteSpecificationResponse = await client.DeleteAsync($"/api/admin/product-specifications/{specificationId}");

        Assert.Equal(HttpStatusCode.OK, deleteImageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleteSpecificationResponse.StatusCode);

        using var listAfterDeleteResponse = await client.GetAsync("/api/admin/products");
        var listAfterDeleteJson = await listAfterDeleteResponse.ReadJsonAsync();

        Assert.Empty(listAfterDeleteJson["data"]!["items"]![0]!["images"]!.AsArray());
        Assert.Empty(listAfterDeleteJson["data"]!["items"]![0]!["specifications"]!.AsArray());
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
