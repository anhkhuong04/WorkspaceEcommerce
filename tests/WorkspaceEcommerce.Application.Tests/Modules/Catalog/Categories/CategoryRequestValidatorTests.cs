using WorkspaceEcommerce.Application.Modules.Catalog.Categories;

namespace WorkspaceEcommerce.Application.Tests.Modules.Catalog.Categories;

public sealed class CategoryRequestValidatorTests
{
    [Fact]
    public void CreateCategoryRequestValidator_ValidRequest_IsValid()
    {
        var validator = new CreateCategoryRequestValidator();
        var request = new CreateCategoryRequest
        {
            Name = new Dictionary<string, string> { { "en", "Desk Accessories" } },
            Slug = "desk-accessories",
            SortOrder = 1,
            IsActive = true
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateCategoryRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var validator = new CreateCategoryRequestValidator();
        var request = new CreateCategoryRequest
        {
            Name = new Dictionary<string, string>(),
            Slug = "Invalid Slug",
            SortOrder = -1
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCategoryRequest.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCategoryRequest.Slug));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCategoryRequest.SortOrder));
    }

    [Fact]
    public void UpdateCategoryRequestValidator_ValidRequest_IsValid()
    {
        var validator = new UpdateCategoryRequestValidator();
        var request = new UpdateCategoryRequest
        {
            Name = new Dictionary<string, string> { { "en", "Standing Desks" } },
            Slug = "standing-desks",
            SortOrder = 2,
            IsActive = false
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateCategoryRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var validator = new UpdateCategoryRequestValidator();
        var request = new UpdateCategoryRequest
        {
            Name = new Dictionary<string, string>(),
            Slug = "standing_desks",
            SortOrder = -10,
            IsActive = true
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateCategoryRequest.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateCategoryRequest.Slug));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateCategoryRequest.SortOrder));
    }
}
