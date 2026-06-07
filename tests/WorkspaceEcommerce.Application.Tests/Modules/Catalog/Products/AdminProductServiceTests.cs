using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Catalog.Products;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Tests.Modules.Catalog.Products;

public sealed class AdminProductServiceTests
{
    [Fact]
    public async Task CreateProductAsync_ValidRequest_CreatesProduct()
    {
        var category = CreateCategory();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        var service = CreateService(dbContext);

        var result = await service.CreateProductAsync(new CreateProductRequest
        {
            CategoryId = category.Id,
            Name = "Standing Desk",
            Slug = "standing-desk",
            Description = "Adjustable desk",
            IsFeatured = true,
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("standing-desk", result.Value.Slug);
        Assert.Equal(category.Id, result.Value.CategoryId);
        Assert.True(result.Value.IsFeatured);
        Assert.True(result.Value.IsActive);
        Assert.Single(dbContext.Products);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateProductAsync_MissingCategory_ReturnsValidation()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateProductAsync(CreateProductRequest(Guid.NewGuid()));

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Product category does not exist.", result.Errors);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateProductAsync_DuplicateSlug_ReturnsConflict()
    {
        var category = CreateCategory();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(CreateProduct(category.Id, slug: "standing-desk"));
        var service = CreateService(dbContext);

        var result = await service.CreateProductAsync(CreateProductRequest(category.Id, slug: "standing-desk"));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateProductAsync_ValidRequest_UpdatesAndDeactivatesProduct()
    {
        var category = CreateCategory();
        var product = CreateProduct(category.Id, isActive: true, isFeatured: true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        var service = CreateService(dbContext);

        var result = await service.UpdateProductAsync(product.Id, new UpdateProductRequest
        {
            CategoryId = category.Id,
            Name = "Updated Desk",
            Slug = "updated-desk",
            Description = "Updated",
            IsFeatured = false,
            IsActive = false
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated Desk", result.Value.Name);
        Assert.Equal("updated-desk", result.Value.Slug);
        Assert.False(result.Value.IsFeatured);
        Assert.False(result.Value.IsActive);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateProductAsync_MissingProduct_ReturnsNotFound()
    {
        var category = CreateCategory();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        var service = CreateService(dbContext);

        var result = await service.UpdateProductAsync(Guid.NewGuid(), UpdateProductRequest(category.Id));

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task CreateVariantAsync_ValidRequest_CreatesVariant()
    {
        var category = CreateCategory();
        var product = CreateProduct(category.Id);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        var service = CreateService(dbContext);

        var result = await service.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "desk-001",
            Name = "Black 120x60",
            Color = "Black",
            Size = "120x60",
            Price = 1200000m,
            CompareAtPrice = 1500000m,
            StockQuantity = 10,
            RequiresInstallation = true,
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("DESK-001", result.Value.Sku);
        Assert.Equal(product.Id, result.Value.ProductId);
        Assert.True(result.Value.RequiresInstallation);
        Assert.Single(dbContext.ProductVariants);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateVariantAsync_DuplicateSku_ReturnsConflict()
    {
        var category = CreateCategory();
        var product = CreateProduct(category.Id);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        dbContext.Seed(CreateVariant(product.Id, sku: "DESK-001"));
        var service = CreateService(dbContext);

        var result = await service.CreateVariantAsync(product.Id, CreateVariantRequest(sku: "desk-001"));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateVariantAsync_MissingProduct_ReturnsNotFound()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateVariantAsync(Guid.NewGuid(), CreateVariantRequest());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateVariantAsync_ValidRequest_UpdatesAndDeactivatesVariant()
    {
        var category = CreateCategory();
        var product = CreateProduct(category.Id);
        var variant = CreateVariant(product.Id, sku: "DESK-001", isActive: true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        dbContext.Seed(variant);
        var service = CreateService(dbContext);

        var result = await service.UpdateVariantAsync(variant.Id, new UpdateProductVariantRequest
        {
            Sku = "desk-002",
            Name = "White 140x70",
            Color = "White",
            Size = "140x70",
            Price = 1300000m,
            CompareAtPrice = 1500000m,
            StockQuantity = 4,
            RequiresInstallation = false,
            IsActive = false
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("DESK-002", result.Value.Sku);
        Assert.Equal("White 140x70", result.Value.Name);
        Assert.Equal(1300000m, result.Value.Price);
        Assert.Equal(4, result.Value.StockQuantity);
        Assert.False(result.Value.IsActive);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateVariantAsync_DuplicateSku_ReturnsConflict()
    {
        var productId = Guid.NewGuid();
        var existing = CreateVariant(productId, sku: "EXISTING-SKU");
        var target = CreateVariant(productId, sku: "TARGET-SKU");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(existing, target);
        var service = CreateService(dbContext);

        var result = await service.UpdateVariantAsync(target.Id, UpdateVariantRequest(sku: "existing-sku"));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateVariantAsync_MissingVariant_ReturnsNotFound()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.UpdateVariantAsync(Guid.NewGuid(), UpdateVariantRequest());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetProductsAsync_ExistingProducts_ReturnsProductsWithVariantsAndCategoryName()
    {
        var category = CreateCategory(name: "Desks");
        var product = CreateProduct(category.Id, name: "Standing Desk", slug: "standing-desk");
        var variant = CreateVariant(product.Id, sku: "DESK-001");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        dbContext.Seed(variant);
        var service = CreateService(dbContext);

        var result = await service.GetProductsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var dto = Assert.Single(result.Value);
        Assert.Equal("Desks", dto.CategoryName);
        Assert.Single(dto.Variants);
    }

    private static AdminProductService CreateService(FakeAppDbContext dbContext)
    {
        return new AdminProductService(
            dbContext,
            new CreateProductRequestValidator(),
            new UpdateProductRequestValidator(),
            new CreateProductVariantRequestValidator(),
            new UpdateProductVariantRequestValidator());
    }

    private static Category CreateCategory(string name = "Desks")
    {
        return new Category(Guid.NewGuid(), null, name, "desks", 1, true);
    }

    private static Product CreateProduct(
        Guid categoryId,
        string name = "Standing Desk",
        string slug = "standing-desk",
        bool isActive = true,
        bool isFeatured = false)
    {
        return new Product(Guid.NewGuid(), categoryId, name, slug, "Description", isFeatured, isActive);
    }

    private static ProductVariant CreateVariant(
        Guid productId,
        string sku = "DESK-001",
        bool isActive = true)
    {
        return new ProductVariant(
            Guid.NewGuid(),
            productId,
            sku,
            "Black 120x60",
            "Black",
            "120x60",
            1200000m,
            1500000m,
            10,
            requiresInstallation: true,
            isActive);
    }

    private static CreateProductRequest CreateProductRequest(Guid categoryId, string slug = "standing-desk")
    {
        return new CreateProductRequest
        {
            CategoryId = categoryId,
            Name = "Standing Desk",
            Slug = slug,
            Description = "Description",
            IsFeatured = false,
            IsActive = true
        };
    }

    private static UpdateProductRequest UpdateProductRequest(Guid categoryId, string slug = "standing-desk")
    {
        return new UpdateProductRequest
        {
            CategoryId = categoryId,
            Name = "Standing Desk",
            Slug = slug,
            Description = "Description",
            IsFeatured = false,
            IsActive = true
        };
    }

    private static CreateProductVariantRequest CreateVariantRequest(string sku = "DESK-001")
    {
        return new CreateProductVariantRequest
        {
            Sku = sku,
            Name = "Black 120x60",
            Color = "Black",
            Size = "120x60",
            Price = 1200000m,
            CompareAtPrice = 1500000m,
            StockQuantity = 10,
            RequiresInstallation = true,
            IsActive = true
        };
    }

    private static UpdateProductVariantRequest UpdateVariantRequest(string sku = "DESK-001")
    {
        return new UpdateProductVariantRequest
        {
            Sku = sku,
            Name = "Black 120x60",
            Color = "Black",
            Size = "120x60",
            Price = 1200000m,
            CompareAtPrice = 1500000m,
            StockQuantity = 10,
            RequiresInstallation = true,
            IsActive = true
        };
    }
}
