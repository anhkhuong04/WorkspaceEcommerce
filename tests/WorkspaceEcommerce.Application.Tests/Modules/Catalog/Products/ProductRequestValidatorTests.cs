using WorkspaceEcommerce.Application.Modules.Catalog.Products;

namespace WorkspaceEcommerce.Application.Tests.Modules.Catalog.Products;

public sealed class ProductRequestValidatorTests
{
    [Fact]
    public void CreateProductRequestValidator_ValidRequest_IsValid()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = new Dictionary<string, string> { { "en", "Standing Desk" } },
            Slug = "standing-desk",
            Description = new Dictionary<string, string> { { "en", "Adjustable desk" } },
            IsFeatured = true,
            IsActive = true
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateProductRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest
        {
            CategoryId = Guid.Empty,
            Name = new Dictionary<string, string>(),
            Slug = "Invalid Slug"
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.CategoryId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.Slug));
    }

    [Fact]
    public void UpdateProductRequestValidator_ValidRequest_IsValid()
    {
        var validator = new UpdateProductRequestValidator();
        var request = new UpdateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = new Dictionary<string, string> { { "en", "Monitor Arm" } },
            Slug = "monitor-arm",
            IsFeatured = false,
            IsActive = false
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ProductVariantRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var createValidator = new CreateProductVariantRequestValidator();
        var updateValidator = new UpdateProductVariantRequestValidator();
        var createRequest = new CreateProductVariantRequest
        {
            Sku = "Invalid SKU!",
            Name = string.Empty,
            Price = -1m,
            CompareAtPrice = -2m,
            StockQuantity = -1
        };
        var updateRequest = new UpdateProductVariantRequest
        {
            Sku = createRequest.Sku,
            Name = createRequest.Name,
            Price = createRequest.Price,
            CompareAtPrice = createRequest.CompareAtPrice,
            StockQuantity = createRequest.StockQuantity
        };

        var createResult = createValidator.Validate(createRequest);
        var updateResult = updateValidator.Validate(updateRequest);

        Assert.False(createResult.IsValid);
        Assert.False(updateResult.IsValid);
    }

    [Fact]
    public void ProductImageRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var createValidator = new CreateProductImageRequestValidator();
        var updateValidator = new UpdateProductImageRequestValidator();
        var createRequest = new CreateProductImageRequest
        {
            ImageUrl = string.Empty,
            AltText = new string('a', 251)
        };
        var updateRequest = new UpdateProductImageRequest
        {
            ImageUrl = createRequest.ImageUrl,
            AltText = createRequest.AltText
        };

        var createResult = createValidator.Validate(createRequest);
        var updateResult = updateValidator.Validate(updateRequest);

        Assert.False(createResult.IsValid);
        Assert.False(updateResult.IsValid);
        Assert.Contains(createResult.Errors, error => error.PropertyName == nameof(CreateProductImageRequest.ImageUrl));
        Assert.Contains(createResult.Errors, error => error.PropertyName == nameof(CreateProductImageRequest.AltText));
        Assert.Contains(updateResult.Errors, error => error.PropertyName == nameof(UpdateProductImageRequest.ImageUrl));
        Assert.Contains(updateResult.Errors, error => error.PropertyName == nameof(UpdateProductImageRequest.AltText));
    }

    [Fact]
    public void ProductSpecificationRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var createValidator = new CreateProductSpecificationRequestValidator();
        var updateValidator = new UpdateProductSpecificationRequestValidator();
        var createRequest = new CreateProductSpecificationRequest
        {
            Name = string.Empty,
            Value = string.Empty
        };
        var updateRequest = new UpdateProductSpecificationRequest
        {
            Name = createRequest.Name,
            Value = createRequest.Value
        };

        var createResult = createValidator.Validate(createRequest);
        var updateResult = updateValidator.Validate(updateRequest);

        Assert.False(createResult.IsValid);
        Assert.False(updateResult.IsValid);
        Assert.Contains(createResult.Errors, error => error.PropertyName == nameof(CreateProductSpecificationRequest.Name));
        Assert.Contains(createResult.Errors, error => error.PropertyName == nameof(CreateProductSpecificationRequest.Value));
        Assert.Contains(updateResult.Errors, error => error.PropertyName == nameof(UpdateProductSpecificationRequest.Name));
        Assert.Contains(updateResult.Errors, error => error.PropertyName == nameof(UpdateProductSpecificationRequest.Value));
    }
}
