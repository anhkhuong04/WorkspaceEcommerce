using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Tests.Modules.Customers;

public sealed class CustomerAuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_WhenRequestIsValid_CreatesCustomerAndReturnsToken()
    {
        var dbContext = new FakeAppDbContext();
        var tokenGenerator = new StubTokenGenerator();
        var service = CreateService(dbContext, tokenGenerator: tokenGenerator);

        var result = await service.RegisterAsync(new CustomerRegisterRequest(
            " Nguyen Van A ",
            " 0900000000 ",
            " CUSTOMER@EXAMPLE.COM ",
            "customer-password"));

        Assert.True(result.IsSuccess);
        Assert.Equal("customer@example.com", result.Value!.Email);
        Assert.Equal("Nguyen Van A", result.Value.FullName);
        Assert.Equal("0900000000", result.Value.PhoneNumber);
        Assert.Single(dbContext.Customers);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Equal(dbContext.Customers.Single().Id, tokenGenerator.LastCustomerId);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsConflict()
    {
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(new Customer(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "hash"));
        var service = CreateService(dbContext);

        var result = await service.RegisterAsync(new CustomerRegisterRequest(
            "Nguyen Van B",
            "0911111111",
            " CUSTOMER@EXAMPLE.COM ",
            "customer-password"));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Customer email is already registered.", result.Errors);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsToken()
    {
        var dbContext = new FakeAppDbContext();
        var customer = new Customer(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            StubPasswordHasher.ValidHash);
        dbContext.Seed(customer);
        var tokenGenerator = new StubTokenGenerator();
        var service = CreateService(dbContext, tokenGenerator: tokenGenerator);

        var result = await service.LoginAsync(new CustomerLoginRequest(
            " CUSTOMER@EXAMPLE.COM ",
            StubPasswordHasher.ValidPassword));

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value!.CustomerId);
        Assert.Equal(customer.Id, tokenGenerator.LastCustomerId);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsInvalid_ReturnsUnauthorized()
    {
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(new Customer(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            StubPasswordHasher.ValidHash));
        var service = CreateService(dbContext);

        var result = await service.LoginAsync(new CustomerLoginRequest(
            "customer@example.com",
            "wrong-password"));

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Contains("Invalid email or password.", result.Errors);
    }

    [Fact]
    public async Task RegisterAsync_WhenRequestIsInvalid_ReturnsValidation()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.RegisterAsync(new CustomerRegisterRequest(
            string.Empty,
            string.Empty,
            "not-an-email",
            "short"));

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Empty(dbContext.Customers);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    private static CustomerAuthService CreateService(
        FakeAppDbContext dbContext,
        StubTokenGenerator? tokenGenerator = null)
    {
        return new CustomerAuthService(
            dbContext,
            new CustomerRegisterRequestValidator(),
            new CustomerLoginRequestValidator(),
            new StubPasswordHasher(),
            tokenGenerator ?? new StubTokenGenerator());
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public const string ValidPassword = "customer-password";
        public const string ValidHash = "hashed-customer-password";

        public string Hash(string password)
        {
            return password == ValidPassword ? ValidHash : $"hashed-{password}";
        }

        public bool Verify(string password, string passwordHash)
        {
            return password == ValidPassword && passwordHash == ValidHash;
        }
    }

    private sealed class StubTokenGenerator : IJwtTokenGenerator
    {
        public Guid? LastCustomerId { get; private set; }

        public AdminLoginResponse GenerateAdminToken(string email)
        {
            throw new NotSupportedException();
        }

        public CustomerAuthResponse GenerateCustomerToken(
            Guid customerId,
            string email,
            string fullName,
            string phoneNumber)
        {
            LastCustomerId = customerId;

            return new CustomerAuthResponse(
                "customer-token",
                "Bearer",
                DateTimeOffset.UtcNow.AddHours(1),
                customerId,
                email,
                fullName,
                phoneNumber);
        }
    }
}
