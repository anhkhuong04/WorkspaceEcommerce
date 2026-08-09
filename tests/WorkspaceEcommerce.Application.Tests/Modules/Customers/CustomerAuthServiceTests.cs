using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;
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
        dbContext.Seed(Customer.Create(
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
        var customer = Customer.Create(
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
        dbContext.Seed(Customer.Create(
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

    [Fact]
    public async Task LoginWithGoogleAsync_WhenTokenIsRejected_ReturnsGenericUnauthorizedResponse()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.LoginWithGoogleAsync(new CustomerGoogleLoginRequest("rejected-token"));

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Equal("Google authentication failed.", result.FirstError);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenVerifiedIdentityMatchesPasswordAccount_LinksAccount()
    {
        var dbContext = new FakeAppDbContext();
        var customer = Customer.Create(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            StubPasswordHasher.ValidHash);
        dbContext.Seed(customer);
        var service = CreateService(
            dbContext,
            googleIdTokenValidator: new StubGoogleIdTokenValidator(
                new GoogleIdentity("google-subject", "customer@example.com", "Google Customer", null)));

        var result = await service.LoginWithGoogleAsync(new CustomerGoogleLoginRequest("verified-token"));

        Assert.True(result.IsSuccess);
        Assert.Equal("google-subject", customer.GoogleId);
        Assert.True(customer.IsEmailVerified);
        Assert.Single(dbContext.Customers);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenEmailBelongsToAnotherGoogleSubject_RejectsUnsafeLink()
    {
        var dbContext = new FakeAppDbContext();
        var customer = Customer.CreateFromGoogle(
            Guid.NewGuid(),
            "Nguyen Van A",
            "customer@example.com",
            "existing-google-subject");
        dbContext.Seed(customer);
        var service = CreateService(
            dbContext,
            googleIdTokenValidator: new StubGoogleIdTokenValidator(
                new GoogleIdentity("different-google-subject", "customer@example.com", "Google Customer", null)));

        var result = await service.LoginWithGoogleAsync(new CustomerGoogleLoginRequest("verified-token"));

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Equal("existing-google-subject", customer.GoogleId);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenExistingSubjectMatches_ReturnsSameCustomerWithoutDuplicate()
    {
        var dbContext = new FakeAppDbContext();
        var customer = Customer.CreateFromGoogle(
            Guid.NewGuid(),
            "Nguyen Van A",
            "customer@example.com",
            "google-subject");
        dbContext.Seed(customer);
        var service = CreateService(
            dbContext,
            googleIdTokenValidator: new StubGoogleIdTokenValidator(
                new GoogleIdentity("google-subject", "customer@example.com", "Google Customer", null)));

        var result = await service.LoginWithGoogleAsync(new CustomerGoogleLoginRequest("verified-token"));

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value!.CustomerId);
        Assert.Single(dbContext.Customers);
    }

    [Fact]
    public async Task LoginAsync_WhenTwoFactorServiceRequiresChallenge_DoesNotIssueAccessToken()
    {
        var dbContext = new FakeAppDbContext();
        var customer = Customer.Create(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            StubPasswordHasher.ValidHash);
        dbContext.Seed(customer);
        var challenge = CustomerAuthResponse.TwoFactorRequired(
            customer.Id,
            customer.Email,
            customer.FullName,
            customer.PhoneNumber!,
            "challenge-token");
        var service = CreateService(dbContext, twoFactorService: new StubTwoFactorService(challenge));

        var result = await service.LoginAsync(new CustomerLoginRequest(
            "customer@example.com",
            StubPasswordHasher.ValidPassword));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RequiresTwoFactor);
        Assert.Null(result.Value.AccessToken);
        Assert.Equal("challenge-token", result.Value.TwoFactorChallengeToken);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenTwoFactorServiceRequiresChallenge_DoesNotIssueAccessToken()
    {
        var dbContext = new FakeAppDbContext();
        var customer = Customer.CreateFromGoogle(
            Guid.NewGuid(),
            "Nguyen Van A",
            "customer@example.com",
            "google-subject");
        dbContext.Seed(customer);
        var challenge = CustomerAuthResponse.TwoFactorRequired(
            customer.Id,
            customer.Email,
            customer.FullName,
            customer.PhoneNumber ?? string.Empty,
            "challenge-token");
        var service = CreateService(
            dbContext,
            googleIdTokenValidator: new StubGoogleIdTokenValidator(
                new GoogleIdentity("google-subject", "customer@example.com", "Google Customer", null)),
            twoFactorService: new StubTwoFactorService(challenge));

        var result = await service.LoginWithGoogleAsync(new CustomerGoogleLoginRequest("verified-token"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RequiresTwoFactor);
        Assert.Null(result.Value.AccessToken);
        Assert.Equal("challenge-token", result.Value.TwoFactorChallengeToken);
    }

    private static CustomerAuthService CreateService(
        FakeAppDbContext dbContext,
        StubTokenGenerator? tokenGenerator = null,
        IGoogleIdTokenValidator? googleIdTokenValidator = null,
        ICustomerTwoFactorService? twoFactorService = null)
    {
        var generator = tokenGenerator ?? new StubTokenGenerator();
        return new CustomerAuthService(
            dbContext,
            new CustomerRegisterRequestValidator(),
            new CustomerLoginRequestValidator(),
            new StubPasswordHasher(),
            new StubCurrentCustomerContext(),
            googleIdTokenValidator ?? new StubGoogleIdTokenValidator(),
            twoFactorService ?? new StubTwoFactorService(),
            new StubCustomerSessionService(dbContext, generator),
            new StubAccountLifecycleService());
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
            string? phoneNumber)
        {
            LastCustomerId = customerId;

            return new CustomerAuthResponse(
                "customer-token",
                "Bearer",
                DateTimeOffset.UtcNow.AddHours(1),
                customerId,
                email,
                fullName,
                phoneNumber ?? string.Empty);
        }
    }

    private sealed class StubGoogleIdTokenValidator(GoogleIdentity? identity = null) : IGoogleIdTokenValidator
    {
        public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(identity);
        }
    }

    private sealed class StubCustomerSessionService(
        FakeAppDbContext dbContext,
        IJwtTokenGenerator tokenGenerator) : ICustomerSessionService
    {
        public async Task<CustomerAuthResponse> IssueAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return tokenGenerator.GenerateCustomerToken(customer.Id, customer.Email, customer.FullName, customer.PhoneNumber)
                with { RefreshToken = "test-refresh-token" };
        }

        public Task<Result<CustomerAuthResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerAuthResponse>.Unauthorized("Not used."));

        public Task RevokeAsync(string refreshToken, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevokeAllAsync(Guid customerId, string reason, CancellationToken cancellationToken = default) =>
            dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class StubAccountLifecycleService : ICustomerAccountLifecycleService
    {
        public Task QueueEmailVerificationAsync(Customer customer, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RevokeOutstandingPasswordResetTokensAsync(
            Guid customerId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Result> RequestEmailVerificationAsync(RequestEmailVerificationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> ConfirmEmailVerificationAsync(ConfirmEmailVerificationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class StubTwoFactorService(CustomerAuthResponse? challenge = null) : ICustomerTwoFactorService
    {
        public Task<Result<TwoFactorSetupStartResponse>> StartSetupAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<TwoFactorSetupStartResponse>.Failure("Not used."));

        public Task<Result<TwoFactorSetupConfirmationResponse>> ConfirmSetupAsync(
            ConfirmTwoFactorSetupRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<TwoFactorSetupConfirmationResponse>.Failure("Not used."));

        public Task<Result> DisableAsync(DisableTwoFactorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure("Not used."));

        public Task<CustomerAuthResponse?> CreateLoginChallengeAsync(
            Customer customer,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(challenge);

        public Task<Result<CustomerAuthResponse>> VerifyLoginAsync(
            VerifyTwoFactorLoginRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerAuthResponse>.Failure("Not used."));

        public Task<Result<CustomerAuthResponse>> VerifyRecoveryAsync(
            VerifyTwoFactorRecoveryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerAuthResponse>.Failure("Not used."));
    }

    private sealed class StubCurrentCustomerContext : WorkspaceEcommerce.Application.Abstractions.Authentication.ICurrentCustomerContext
    {
        public Guid? CustomerId => Guid.NewGuid();
        public string? Email => null;
    }
}
