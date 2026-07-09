using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class CheckoutRequestValidatorTests
{
    [Fact]
    public void CheckoutRequestValidator_ValidRequest_IsValid()
    {
        var validator = new CheckoutRequestValidator();
        var request = CreateRequest();

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CheckoutRequestValidator_InvalidRequest_HasValidationErrors()
    {
        var validator = new CheckoutRequestValidator();
        var request = new CheckoutRequest
        {
            SessionId = string.Empty,
            CustomerName = string.Empty,
            CustomerPhone = string.Empty,
            CustomerEmail = "invalid-email",
            ShippingAddress = string.Empty,
            ShippingStreet = string.Empty,
            ShippingWard = string.Empty,
            ShippingProvince = string.Empty,
            PaymentMethod = (PaymentMethod)999
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.SessionId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.CustomerName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.CustomerPhone));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.CustomerEmail));
        // ShippingAddress is no longer strictly validated since we use structured fields
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.ShippingStreet));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.ShippingWard));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.ShippingProvince));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CheckoutRequest.PaymentMethod));
    }

    private static CheckoutRequest CreateRequest()
    {
        return new CheckoutRequest
        {
            SessionId = "session-1",
            CustomerName = "Nguyen Van A",
            CustomerPhone = "0900000000",
            CustomerEmail = "customer@example.com",
            ShippingAddress = "123 Shipping Street, Ward 1, District 1, Ho Chi Minh",
            ShippingStreet = "123 Shipping Street",
            ShippingWard = "Ward 1",
            ShippingProvince = "Ho Chi Minh",
            Note = "Call before delivery",
            PaymentMethod = PaymentMethod.Cod
        };
    }
}
