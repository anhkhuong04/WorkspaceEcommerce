using WorkspaceEcommerce.Application.Modules.Cart;

namespace WorkspaceEcommerce.Application.Tests.Modules.Cart;

public sealed class CartRequestValidatorTests
{
    [Fact]
    public void AddCartItemRequestValidator_ValidRequest_IsValid()
    {
        var validator = new AddCartItemRequestValidator();
        var request = new AddCartItemRequest
        {
            SessionId = "session-1",
            ProductVariantId = Guid.NewGuid(),
            Quantity = 1
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AddCartItemRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var validator = new AddCartItemRequestValidator();
        var request = new AddCartItemRequest
        {
            SessionId = string.Empty,
            ProductVariantId = Guid.Empty,
            Quantity = 0
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddCartItemRequest.SessionId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddCartItemRequest.ProductVariantId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddCartItemRequest.Quantity));
    }

    [Fact]
    public void UpdateCartItemRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var validator = new UpdateCartItemRequestValidator();
        var request = new UpdateCartItemRequest
        {
            SessionId = string.Empty,
            Quantity = 0
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateCartItemRequest.SessionId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateCartItemRequest.Quantity));
    }

    [Fact]
    public void GetAndRemoveCartRequestValidators_EmptySession_HaveValidationErrors()
    {
        var getResult = new GetCartRequestValidator().Validate(new GetCartRequest());
        var removeResult = new RemoveCartItemRequestValidator().Validate(new RemoveCartItemRequest());

        Assert.False(getResult.IsValid);
        Assert.False(removeResult.IsValid);
        Assert.Contains(getResult.Errors, error => error.PropertyName == nameof(GetCartRequest.SessionId));
        Assert.Contains(removeResult.Errors, error => error.PropertyName == nameof(RemoveCartItemRequest.SessionId));
    }
}
