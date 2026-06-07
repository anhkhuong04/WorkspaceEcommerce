using WorkspaceEcommerce.Application.Modules.Admin.Authentication;

namespace WorkspaceEcommerce.Application.Tests.Modules.Admin.Authentication;

public sealed class AdminLoginRequestValidatorTests
{
    [Fact]
    public void Validate_WhenRequestIsValid_IsValid()
    {
        var validator = new AdminLoginRequestValidator();
        var request = new AdminLoginRequest("admin@example.com", "strong-password");

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenRequestIsInvalid_HasValidationErrors()
    {
        var validator = new AdminLoginRequestValidator();
        var request = new AdminLoginRequest("not-an-email", string.Empty);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AdminLoginRequest.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AdminLoginRequest.Password));
    }
}
