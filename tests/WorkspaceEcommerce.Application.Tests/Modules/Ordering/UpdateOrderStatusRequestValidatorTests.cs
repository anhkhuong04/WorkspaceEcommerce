using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class UpdateOrderStatusRequestValidatorTests
{
    private readonly UpdateOrderStatusRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var request = new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Confirmed,
            Note = "Confirmed by admin"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidStatus_IsInvalid()
    {
        var request = new UpdateOrderStatusRequest
        {
            Status = (OrderStatus)999
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateOrderStatusRequest.Status));
    }
}
