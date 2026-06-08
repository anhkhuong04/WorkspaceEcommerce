using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class AdminOrderListRequestValidatorTests
{
    private readonly AdminOrderListRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidStatus_IsValid()
    {
        var request = new AdminOrderListRequest
        {
            Status = OrderStatus.Pending
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidStatus_IsInvalid()
    {
        var request = new AdminOrderListRequest
        {
            Status = (OrderStatus)999
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AdminOrderListRequest.Status));
    }
}
