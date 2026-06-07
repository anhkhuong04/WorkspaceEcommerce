using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Tests.Modules.Catalog.Storefront;

public sealed class StorefrontCatalogServiceTests
{
    [Fact]
    public async Task GetCategoriesAsync_OnlyActiveCategories_ReturnsActiveTree()
    {
        var root = CreateCategory(name: "Desks", slug: "desks", isActive: true);
        var child = CreateCategory(parentId: root.Id, name: "Standing Desks", slug: "standing-desks", isActive: true);
        var inactive = CreateCategory(name: "Hidden", slug: "hidden", isActive: false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(root, child, inactive);
        var service = new StorefrontCatalogService(dbContext);

        var result = await service.GetCategoriesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var category = Assert.Single(result.Value);
        Assert.Equal(root.Id, category.Id);
        Assert.Single(category.Children);
        Assert.DoesNotContain(result.Value, item => item.Id == inactive.Id);
    }

    [Fact]
    public async Task GetProductsAsync_OnlyActiveProductCategoryAndVariant_ReturnsVisibleProducts()
    {
        var activeCategory = CreateCategory(name: "Desks", slug: "desks", isActive: true);
        var inactiveCategory = CreateCategory(name: "Hidden", slug: "hidden", isActive: false);
        var visibleProduct = CreateProduct(activeCategory.Id, name: "Standing Desk", slug: "standing-desk", isActive: true);
        var inactiveProduct = CreateProduct(activeCategory.Id, name: "Hidden Product", slug: "hidden-product", isActive: false);
        var hiddenCategoryProduct = CreateProduct(inactiveCategory.Id, name: "Hidden Category Product", slug: "hidden-category-product", isActive: true);
        var activeVariant = CreateVariant(visibleProduct.Id, sku: "DESK-001", isActive: true, stockQuantity: 5);
        var inactiveVariant = CreateVariant(visibleProduct.Id, sku: "DESK-002", isActive: false, stockQuantity: 10);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(activeCategory, inactiveCategory);
        dbContext.Seed(visibleProduct, inactiveProduct, hiddenCategoryProduct);
        dbContext.Seed(activeVariant, inactiveVariant);
        var service = new StorefrontCatalogService(dbContext);

        var result = await service.GetProductsAsync(new ProductListRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(visibleProduct.Id, item.Id);
        Assert.Equal(activeVariant.Price, item.MinPrice);
        Assert.True(item.IsInStock);
    }

    [Fact]
    public async Task GetProductsAsync_FilterAndPagination_ReturnsExpectedPage()
    {
        var desks = CreateCategory(name: "Desks", slug: "desks");
        var chairs = CreateCategory(name: "Chairs", slug: "chairs");
        var first = CreateProduct(desks.Id, name: "Alpha Desk", slug: "alpha-desk");
        var second = CreateProduct(desks.Id, name: "Beta Desk", slug: "beta-desk");
        var chair = CreateProduct(chairs.Id, name: "Alpha Chair", slug: "alpha-chair");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(desks, chairs);
        dbContext.Seed(first, second, chair);
        dbContext.Seed(
            CreateVariant(first.Id, sku: "A", price: 100m, stockQuantity: 1),
            CreateVariant(second.Id, sku: "B", price: 200m, stockQuantity: 0),
            CreateVariant(chair.Id, sku: "C", price: 100m, stockQuantity: 1));
        var service = new StorefrontCatalogService(dbContext);

        var result = await service.GetProductsAsync(new ProductListRequest
        {
            CategorySlug = "desks",
            Search = "desk",
            MinPrice = 50m,
            MaxPrice = 250m,
            InStock = true,
            PageNumber = 1,
            PageSize = 1
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(first.Id, Assert.Single(result.Value.Items).Id);
    }

    [Fact]
    public async Task GetProductBySlugAsync_ActiveProduct_ReturnsDetailWithActiveVariantsImagesAndSpecifications()
    {
        var category = CreateCategory(name: "Desks", slug: "desks");
        var product = CreateProduct(category.Id, name: "Standing Desk", slug: "standing-desk");
        var activeVariant = CreateVariant(product.Id, sku: "DESK-001", isActive: true);
        var inactiveVariant = CreateVariant(product.Id, sku: "DESK-002", isActive: false);
        var image = new ProductImage(Guid.NewGuid(), product.Id, "/images/desk.jpg", "Desk", 1);
        var specification = new ProductSpecification(Guid.NewGuid(), product.Id, "Material", "Wood", 1);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        dbContext.Seed(activeVariant, inactiveVariant);
        dbContext.Seed(image);
        dbContext.Seed(specification);
        var service = new StorefrontCatalogService(dbContext);

        var result = await service.GetProductBySlugAsync("standing-desk");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(product.Id, result.Value.Id);
        Assert.Equal("Desks", result.Value.CategoryName);
        Assert.Single(result.Value.Variants);
        Assert.Equal(activeVariant.Id, result.Value.Variants.Single().Id);
        Assert.Single(result.Value.Images);
        Assert.Single(result.Value.Specifications);
    }

    [Fact]
    public async Task GetProductBySlugAsync_InactiveProduct_ReturnsNotFound()
    {
        var category = CreateCategory();
        var product = CreateProduct(category.Id, slug: "hidden-product", isActive: false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        var service = new StorefrontCatalogService(dbContext);

        var result = await service.GetProductBySlugAsync("hidden-product");

        Assert.False(result.IsSuccess);
        Assert.Contains("Product was not found.", result.Errors);
    }

    [Fact]
    public async Task GetProductBySlugAsync_InactiveCategory_ReturnsNotFound()
    {
        var category = CreateCategory(isActive: false);
        var product = CreateProduct(category.Id, slug: "standing-desk", isActive: true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        var service = new StorefrontCatalogService(dbContext);

        var result = await service.GetProductBySlugAsync("standing-desk");

        Assert.False(result.IsSuccess);
        Assert.Contains("Product was not found.", result.Errors);
    }

    private static Category CreateCategory(
        Guid? parentId = null,
        string name = "Category",
        string slug = "category",
        bool isActive = true)
    {
        return new Category(Guid.NewGuid(), parentId, name, slug, 1, isActive);
    }

    private static Product CreateProduct(
        Guid categoryId,
        string name = "Product",
        string slug = "product",
        bool isActive = true)
    {
        return new Product(Guid.NewGuid(), categoryId, name, slug, "Description", false, isActive);
    }

    private static ProductVariant CreateVariant(
        Guid productId,
        string sku = "SKU-001",
        decimal price = 100m,
        int stockQuantity = 10,
        bool isActive = true)
    {
        return new ProductVariant(
            Guid.NewGuid(),
            productId,
            sku,
            "Default",
            null,
            null,
            price,
            null,
            stockQuantity,
            requiresInstallation: false,
            isActive);
    }
}
