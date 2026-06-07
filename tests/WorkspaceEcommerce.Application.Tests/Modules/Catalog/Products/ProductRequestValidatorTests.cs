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
            Name = "Standing Desk",
            Slug = "standing-desk",
            Description = "Adjustable desk",
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
            Name = string.Empty,
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
            Name = "Monitor Arm",
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
}
