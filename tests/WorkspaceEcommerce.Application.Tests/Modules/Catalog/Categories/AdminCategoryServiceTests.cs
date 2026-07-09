using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Catalog.Categories;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Tests.Modules.Catalog.Categories;

public sealed class AdminCategoryServiceTests
{
    [Fact]
    public async Task DeleteCategoryAsync_EmptyCategory_RemovesCategory()
    {
        var category = CreateCategory();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        var service = CreateService(dbContext);

        var result = await service.DeleteCategoryAsync(category.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(dbContext.Categories);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteCategoryAsync_CategoryWithChild_ReturnsConflict()
    {
        var category = CreateCategory();
        var child = CreateCategory(parentId: category.Id, slug: "child");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category, child);
        var service = CreateService(dbContext);

        var result = await service.DeleteCategoryAsync(category.Id);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(2, dbContext.Categories.Count());
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteCategoryAsync_CategoryWithProduct_ReturnsConflict()
    {
        var category = CreateCategory();
        var product = new Product(Guid.NewGuid(), category.Id, WorkspaceEcommerce.Domain.Common.LocalizedText.Of("Desk"), "desk", null, false, true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        dbContext.Seed(product);
        var service = CreateService(dbContext);

        var result = await service.DeleteCategoryAsync(category.Id);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Single(dbContext.Categories);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCategoryAsync_ValidRequest_CreatesCategory()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);
        var request = new CreateCategoryRequest
        {
            Name = new Dictionary<string, string> { ["en"] = "Desk Accessories" },
            Slug = "desk-accessories",
            SortOrder = 3,
            IsActive = true
        };

        var result = await service.CreateCategoryAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("desk-accessories", result.Value.Slug);
        Assert.Equal("Desk Accessories", result.Value.Name["en"]);
        Assert.True(result.Value.IsActive);
        Assert.Single(dbContext.Categories);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCategoryAsync_DuplicateSlug_ReturnsConflict()
    {
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateCategory(slug: "desk-accessories"));
        var service = CreateService(dbContext);
        var request = new CreateCategoryRequest
        {
            Name = new Dictionary<string, string> { ["en"] = "Desk Accessories" },
            Slug = "desk-accessories",
            SortOrder = 1,
            IsActive = true
        };

        var result = await service.CreateCategoryAsync(request);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCategoryAsync_InvalidParent_ReturnsValidation()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);
        var request = new CreateCategoryRequest
        {
            ParentId = Guid.NewGuid(),
            Name = new Dictionary<string, string> { ["en"] = "Monitor Arms" },
            Slug = "monitor-arms",
            SortOrder = 1,
            IsActive = true
        };

        var result = await service.CreateCategoryAsync(request);

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Parent category does not exist.", result.Errors);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateCategoryAsync_ValidRequest_UpdatesAndDeactivatesCategory()
    {
        var category = CreateCategory(name: "Old Name", slug: "old-name", isActive: true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        var service = CreateService(dbContext);
        var request = new UpdateCategoryRequest
        {
            Name = new Dictionary<string, string> { ["en"] = "New Name" },
            Slug = "new-name",
            SortOrder = 9,
            IsActive = false
        };

        var result = await service.UpdateCategoryAsync(category.Id, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("New Name", result.Value.Name["en"]);
        Assert.Equal("new-name", result.Value.Slug);
        Assert.Equal(9, result.Value.SortOrder);
        Assert.False(result.Value.IsActive);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateCategoryAsync_InactiveCategoryCanBeActivated_ActivatesCategory()
    {
        var category = CreateCategory(isActive: false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        var service = CreateService(dbContext);
        var request = new UpdateCategoryRequest
        {
            Name = category.Name,
            Slug = category.Slug,
            SortOrder = category.SortOrder,
            IsActive = true
        };

        var result = await service.UpdateCategoryAsync(category.Id, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsActive);
        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task UpdateCategoryAsync_DuplicateSlug_ReturnsConflict()
    {
        var existing = CreateCategory(slug: "existing-category");
        var target = CreateCategory(slug: "target-category");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(existing, target);
        var service = CreateService(dbContext);
        var request = new UpdateCategoryRequest
        {
            Name = new Dictionary<string, string> { ["en"] = "Target Category" },
            Slug = existing.Slug,
            SortOrder = 1,
            IsActive = true
        };

        var result = await service.UpdateCategoryAsync(target.Id, request);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateCategoryAsync_MissingCategory_ReturnsNotFound()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);
        var request = new UpdateCategoryRequest
        {
            Name = new Dictionary<string, string> { ["en"] = "Missing" },
            Slug = "missing",
            SortOrder = 1,
            IsActive = true
        };

        var result = await service.UpdateCategoryAsync(Guid.NewGuid(), request);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateCategoryAsync_SelfParent_ReturnsValidation()
    {
        var category = CreateCategory();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        var service = CreateService(dbContext);
        var request = new UpdateCategoryRequest
        {
            ParentId = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            SortOrder = category.SortOrder,
            IsActive = true
        };

        var result = await service.UpdateCategoryAsync(category.Id, request);

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Category cannot be its own parent.", result.Errors);
    }

    [Fact]
    public async Task UpdateCategoryAsync_InvalidParent_ReturnsValidation()
    {
        var category = CreateCategory();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(category);
        var service = CreateService(dbContext);
        var request = new UpdateCategoryRequest
        {
            ParentId = Guid.NewGuid(),
            Name = category.Name,
            Slug = category.Slug,
            SortOrder = category.SortOrder,
            IsActive = true
        };

        var result = await service.UpdateCategoryAsync(category.Id, request);

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Parent category does not exist.", result.Errors);
    }

    [Fact]
    public async Task UpdateCategoryAsync_ParentWouldCreateCycle_ReturnsValidation()
    {
        var root = CreateCategory(name: "Root", slug: "root");
        var child = CreateCategory(parentId: root.Id, name: "Child", slug: "child");
        var grandChild = CreateCategory(parentId: child.Id, name: "Grand Child", slug: "grand-child");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(root, child, grandChild);
        var service = CreateService(dbContext);
        var request = new UpdateCategoryRequest
        {
            ParentId = grandChild.Id,
            Name = root.Name,
            Slug = root.Slug,
            SortOrder = root.SortOrder,
            IsActive = root.IsActive
        };

        var result = await service.UpdateCategoryAsync(root.Id, request);

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Category parent would create a cycle.", result.Errors);
    }

    [Fact]
    public async Task GetCategoriesAsync_ExistingParentChildCategories_ReturnsTree()
    {
        var root = CreateCategory(name: "Root", slug: "root", sortOrder: 2);
        var anotherRoot = CreateCategory(name: "Another Root", slug: "another-root", sortOrder: 1);
        var child = CreateCategory(parentId: root.Id, name: "Child", slug: "child");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(root, anotherRoot, child);
        var service = CreateService(dbContext);

        var result = await service.GetCategoriesAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Collection(
            result.Value,
            first => Assert.Equal(anotherRoot.Id, first.Id),
            second =>
            {
                Assert.Equal(root.Id, second.Id);
                Assert.Single(second.Children);
                Assert.Equal(child.Id, second.Children.Single().Id);
            });
    }

    private static AdminCategoryService CreateService(FakeAppDbContext dbContext)
    {
        return new AdminCategoryService(
            dbContext,
            new CreateCategoryRequestValidator(),
            new UpdateCategoryRequestValidator());
    }

    private static Category CreateCategory(
        Guid? parentId = null,
        string name = "Category",
        string slug = "category",
        int sortOrder = 1,
        bool isActive = true)
    {
        return new Category(Guid.NewGuid(), parentId, WorkspaceEcommerce.Domain.Common.LocalizedText.Of(name), slug, sortOrder, isActive);
    }
}
