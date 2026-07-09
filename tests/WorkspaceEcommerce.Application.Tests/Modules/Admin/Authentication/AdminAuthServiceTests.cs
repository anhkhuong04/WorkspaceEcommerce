using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;

namespace WorkspaceEcommerce.Application.Tests.Modules.Admin.Authentication;

public sealed class AdminAuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsToken()
    {
        var token = new AdminLoginResponse("token", "Bearer", DateTimeOffset.UtcNow.AddHours(1), "admin@example.com");
        var service = new AdminAuthService(
            new AdminLoginRequestValidator(),
            new StubCredentialValidator(true),
            new StubTokenGenerator(token));

        var result = await service.LoginAsync(new AdminLoginRequest("admin@example.com", "password"));

        Assert.True(result.IsSuccess);
        Assert.Equal(token, result.Value);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        var service = new AdminAuthService(
            new AdminLoginRequestValidator(),
            new StubCredentialValidator(false),
            new StubTokenGenerator(new AdminLoginResponse("token", "Bearer", DateTimeOffset.UtcNow, "admin@example.com")));

        var result = await service.LoginAsync(new AdminLoginRequest("admin@example.com", "wrong-password"));

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Contains("Invalid email or password.", result.Errors);
    }

    [Fact]
    public async Task LoginAsync_WhenRequestIsInvalid_ReturnsValidation()
    {
        var service = new AdminAuthService(
            new AdminLoginRequestValidator(),
            new StubCredentialValidator(true),
            new StubTokenGenerator(new AdminLoginResponse("token", "Bearer", DateTimeOffset.UtcNow, "admin@example.com")));

        var result = await service.LoginAsync(new AdminLoginRequest("bad-email", string.Empty));

        Assert.Equal(ResultStatus.Validation, result.Status);
    }

    private sealed class StubCredentialValidator(bool isValid) : IAdminCredentialValidator
    {
        public bool IsValid(string email, string password)
        {
            return isValid;
        }
    }

    private sealed class StubTokenGenerator(AdminLoginResponse response) : IJwtTokenGenerator
    {
        public AdminLoginResponse GenerateAdminToken(string email)
        {
            return response;
        }

        public CustomerAuthResponse GenerateCustomerToken(
            Guid customerId,
            string email,
            string fullName,
            string? phoneNumber)
        {
            throw new NotSupportedException();
        }
    }
}
