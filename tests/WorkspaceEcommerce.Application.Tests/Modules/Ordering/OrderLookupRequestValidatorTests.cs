using WorkspaceEcommerce.Application.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class OrderLookupRequestValidatorTests
{
    private readonly OrderLookupRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var request = new OrderLookupRequest
        {
            OrderCode = "ORD-20260608-ABC12345",
            Phone = "0900000000"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingRequiredFields_IsInvalid()
    {
        var request = new OrderLookupRequest();

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OrderLookupRequest.OrderCode));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OrderLookupRequest.Phone));
    }
}
