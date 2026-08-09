using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Tests.Modules.Customers;

public sealed class CustomerTwoFactorServiceTests
{
    [Fact]
    public async Task StartSetupAsync_DoesNotEnableTwoFactorAndPersistsOnlyProtectedSecret()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);

        var result = await service.StartSetupAsync();

        Assert.True(result.IsSuccess);
        Assert.False(customer.TwoFactorEnabled);
        Assert.Equal("protected:BASE32SECRET", customer.PendingTwoFactorSecret);
        Assert.NotNull(customer.TwoFactorSetupExpiresAt);
        Assert.Equal("BASE32SECRET", result.Value!.ManualEntryKey);
        Assert.StartsWith("otpauth://totp/", result.Value.ProvisioningUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmSetupAsync_WithCorrectCode_EnablesTwoFactorAndReturnsOneTimeRecoveryCodes()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);
        await service.StartSetupAsync();

        var result = await service.ConfirmSetupAsync(new ConfirmTwoFactorSetupRequest("123456"));

        Assert.True(result.IsSuccess);
        Assert.True(customer.TwoFactorEnabled);
        Assert.Equal("protected:BASE32SECRET", customer.TwoFactorSecret);
        Assert.Null(customer.PendingTwoFactorSecret);
        Assert.Equal(10, result.Value!.RecoveryCodes.Count);
        Assert.Equal(10, dbContext.CustomerTwoFactorRecoveryCodes.Count());
        Assert.All(dbContext.CustomerTwoFactorRecoveryCodes, code => Assert.StartsWith("hash:", code.CodeHash, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Value.RecoveryCodes[0], dbContext.CustomerTwoFactorRecoveryCodes.Select(code => code.CodeHash));
    }

    [Fact]
    public async Task ConfirmSetupAsync_WithWrongOrExpiredCode_DoesNotEnableTwoFactor()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(dbContext, customer.Id, clock: clock);
        await service.StartSetupAsync();

        var wrongCodeResult = await service.ConfirmSetupAsync(new ConfirmTwoFactorSetupRequest("000000"));
        Assert.Equal(ResultStatus.Unauthorized, wrongCodeResult.Status);
        Assert.False(customer.TwoFactorEnabled);

        clock.Advance(TimeSpan.FromMinutes(11));
        var expiredResult = await service.ConfirmSetupAsync(new ConfirmTwoFactorSetupRequest("123456"));

        Assert.Equal(ResultStatus.Validation, expiredResult.Status);
        Assert.False(customer.TwoFactorEnabled);
        Assert.Null(customer.PendingTwoFactorSecret);
    }

    [Fact]
    public async Task VerifyLoginAsync_WithValidTotp_IssuesTokenAndRejectsReplayedTimeStep()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);
        await service.StartSetupAsync();
        await service.ConfirmSetupAsync(new ConfirmTwoFactorSetupRequest("123456"));
        var firstChallenge = await service.CreateLoginChallengeAsync(customer);

        var verified = await service.VerifyLoginAsync(new VerifyTwoFactorLoginRequest(
            firstChallenge!.TwoFactorChallengeToken!,
            "123456"));

        Assert.True(verified.IsSuccess);
        Assert.NotNull(verified.Value!.AccessToken);
        Assert.False(verified.Value.RequiresTwoFactor);
        Assert.Equal(42, customer.LastTwoFactorTimeStep);

        var secondChallenge = await service.CreateLoginChallengeAsync(customer);
        var replayed = await service.VerifyLoginAsync(new VerifyTwoFactorLoginRequest(
            secondChallenge!.TwoFactorChallengeToken!,
            "123456"));

        Assert.Equal(ResultStatus.Unauthorized, replayed.Status);
    }

    [Fact]
    public async Task VerifyRecoveryAsync_ConsumesCodeExactlyOnce()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);
        await service.StartSetupAsync();
        var setup = await service.ConfirmSetupAsync(new ConfirmTwoFactorSetupRequest("123456"));
        var recoveryCode = setup.Value!.RecoveryCodes[0];

        var firstChallenge = await service.CreateLoginChallengeAsync(customer);
        var verified = await service.VerifyRecoveryAsync(new VerifyTwoFactorRecoveryRequest(
            firstChallenge!.TwoFactorChallengeToken!,
            recoveryCode));
        Assert.True(verified.IsSuccess);
        Assert.Equal(1, dbContext.CustomerTwoFactorRecoveryCodes.Count(code => code.UsedAt.HasValue));

        var secondChallenge = await service.CreateLoginChallengeAsync(customer);
        var replayed = await service.VerifyRecoveryAsync(new VerifyTwoFactorRecoveryRequest(
            secondChallenge!.TwoFactorChallengeToken!,
            recoveryCode));

        Assert.Equal(ResultStatus.Unauthorized, replayed.Status);
    }

    [Fact]
    public async Task DisableAsync_RequiresSecondFactorAndRevokesStoredMaterial()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);
        await service.StartSetupAsync();
        await service.ConfirmSetupAsync(new ConfirmTwoFactorSetupRequest("123456"));

        var invalid = await service.DisableAsync(new DisableTwoFactorRequest("000000", null));
        Assert.Equal(ResultStatus.Unauthorized, invalid.Status);
        Assert.True(customer.TwoFactorEnabled);

        var valid = await service.DisableAsync(new DisableTwoFactorRequest("123456", null));

        Assert.True(valid.IsSuccess);
        Assert.False(customer.TwoFactorEnabled);
        Assert.Null(customer.TwoFactorSecret);
        Assert.Empty(dbContext.CustomerTwoFactorRecoveryCodes);
    }

    private static CustomerTwoFactorService CreateService(
        FakeAppDbContext dbContext,
        Guid customerId,
        TimeProvider? clock = null)
    {
        return new CustomerTwoFactorService(
            dbContext,
            new StubCurrentCustomerContext(customerId),
            new StubPasswordHasher(),
            new StubTotpService(),
            new StubSecretProtector(),
            new TwoFactorOptions
            {
                Issuer = "WorkspaceEcommerce",
                SetupLifetimeMinutes = 10,
                ChallengeLifetimeMinutes = 5,
                RecoveryCodeCount = 10
            },
            clock ?? TimeProvider.System,
            new ConfirmTwoFactorSetupRequestValidator(),
            new DisableTwoFactorRequestValidator(),
            new VerifyTwoFactorLoginRequestValidator(),
            new VerifyTwoFactorRecoveryRequestValidator(),
            new StubCustomerSessionService());
    }

    private static Customer CreateCustomer()
    {
        return Customer.Create(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "password-hash");
    }

    private sealed class StubCurrentCustomerContext(Guid customerId) : ICurrentCustomerContext
    {
        public Guid? CustomerId => customerId;
        public string? Email => "customer@example.com";
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class StubCustomerSessionService : ICustomerSessionService
    {
        public Task<CustomerAuthResponse> IssueAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CustomerAuthResponse(
                "customer-access-token",
                "Bearer",
                DateTimeOffset.UtcNow.AddMinutes(60),
                customer.Id,
                customer.Email,
                customer.FullName,
                customer.PhoneNumber ?? string.Empty)
                { RefreshToken = "test-refresh-token" });
        }

        public Task<Result<CustomerAuthResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerAuthResponse>.Unauthorized("Not used."));

        public Task RevokeAsync(string refreshToken, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevokeAllAsync(Guid customerId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubTotpService : ITotpService
    {
        public string GenerateSecret() => "BASE32SECRET";

        public string CreateProvisioningUri(string secret, string issuer, string accountName) =>
            $"otpauth://totp/{issuer}:{accountName}?secret={secret}";

        public bool TryVerifyCode(string secret, string code, DateTimeOffset timestamp, out long timeStep)
        {
            timeStep = 42;
            return secret == "BASE32SECRET" && code == "123456";
        }
    }

    private sealed class StubSecretProtector : ITwoFactorSecretProtector
    {
        public string Protect(string secret) => $"protected:{secret}";

        public string Unprotect(string protectedSecret) => protectedSecret["protected:".Length..];
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
