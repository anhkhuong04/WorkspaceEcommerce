using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Tests.Modules.Customers;

public sealed class CustomerAccountLifecycleServiceTests
{
    [Fact]
    public async Task RequestEmailVerificationAsync_DoesNotRevealWhetherAnEligibleAccountExists()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var outbox = new CapturingEmailOutbox();
        var service = CreateService(dbContext, outbox);

        var known = await service.RequestEmailVerificationAsync(new RequestEmailVerificationRequest(customer.Email));
        var unknown = await service.RequestEmailVerificationAsync(new RequestEmailVerificationRequest("unknown@example.com"));

        Assert.True(known.IsSuccess);
        Assert.True(unknown.IsSuccess);
        Assert.Empty(known.Errors);
        Assert.Empty(unknown.Errors);
        Assert.Single(outbox.Messages);
        Assert.Single(dbContext.CustomerAccountTokens);
    }

    [Fact]
    public async Task EmailVerificationToken_IsHashedExpiringAndSingleUse()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new TestTimeProvider(now);
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var outbox = new CapturingEmailOutbox();
        var service = CreateService(dbContext, outbox, clock);

        await service.RequestEmailVerificationAsync(new RequestEmailVerificationRequest(customer.Email));
        var rawToken = ExtractToken(outbox.Messages.Single());
        var storedToken = dbContext.CustomerAccountTokens.Single();

        Assert.NotEqual(rawToken, storedToken.TokenHash);
        Assert.Equal(Hash(rawToken), storedToken.TokenHash);
        Assert.False(customer.IsEmailVerified);

        var verified = await service.ConfirmEmailVerificationAsync(new ConfirmEmailVerificationRequest(rawToken));
        var replayed = await service.ConfirmEmailVerificationAsync(new ConfirmEmailVerificationRequest(rawToken));

        Assert.True(verified.IsSuccess);
        Assert.True(customer.IsEmailVerified);
        Assert.NotNull(storedToken.ConsumedAt);
        Assert.Equal(ResultStatus.Unauthorized, replayed.Status);

        await service.RequestEmailVerificationAsync(new RequestEmailVerificationRequest("another@example.com"));
        // An expired direct token is rejected even if its digest matches a row.
        var expiringCustomer = CreateCustomer("expires@example.com");
        dbContext.Seed(expiringCustomer);
        await service.RequestEmailVerificationAsync(new RequestEmailVerificationRequest(expiringCustomer.Email));
        var expiringToken = ExtractToken(outbox.Messages.Last());
        clock.Advance(TimeSpan.FromMinutes(11));
        var expired = await service.ConfirmEmailVerificationAsync(new ConfirmEmailVerificationRequest(expiringToken));
        Assert.Equal(ResultStatus.Unauthorized, expired.Status);
    }

    [Fact]
    public async Task ResetPasswordAsync_ConsumesAllResetTokensAndRevokesAllSessions()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var outbox = new CapturingEmailOutbox();
        var sessions = new RecordingSessionService();
        var service = CreateService(dbContext, outbox, sessionService: sessions);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest(customer.Email));
        var firstRawToken = ExtractToken(outbox.Messages.Single());
        await service.ForgotPasswordAsync(new ForgotPasswordRequest(customer.Email));
        var secondRawToken = ExtractToken(outbox.Messages.Last());

        var reset = await service.ResetPasswordAsync(new ResetPasswordRequest(secondRawToken, "new-customer-password"));
        var firstReplay = await service.ResetPasswordAsync(new ResetPasswordRequest(firstRawToken, "other-password"));
        var secondReplay = await service.ResetPasswordAsync(new ResetPasswordRequest(secondRawToken, "other-password"));

        Assert.True(reset.IsSuccess);
        Assert.Equal("hash:new-customer-password", customer.PasswordHash);
        Assert.All(dbContext.CustomerAccountTokens.Where(token => token.Purpose == CustomerAccountTokenPurpose.PasswordReset), token =>
            Assert.NotNull(token.ConsumedAt));
        Assert.Equal([customer.Id], sessions.RevokedCustomerIds);
        Assert.Equal(ResultStatus.Unauthorized, firstReplay.Status);
        Assert.Equal(ResultStatus.Unauthorized, secondReplay.Status);
    }

    [Fact]
    public void EmailOutboxMessage_WhenDeliveryFails_RemainsRetryableWithoutContainingPlaintextPayload()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new CustomerEmailOutboxMessage(
            Guid.NewGuid(),
            "customer@example.com",
            "Reset password",
            "protected-payload-only",
            now);

        message.ScheduleRetry("Delivery failed (SmtpException).", now.AddMinutes(1));

        Assert.True(message.IsDueAt(now.AddMinutes(1)));
        Assert.Null(message.SentAt);
        Assert.Equal(1, message.AttemptCount);
        Assert.DoesNotContain("token", message.ProtectedPayload, StringComparison.OrdinalIgnoreCase);
    }

    private static CustomerAccountLifecycleService CreateService(
        FakeAppDbContext dbContext,
        CapturingEmailOutbox outbox,
        TimeProvider? clock = null,
        ICustomerSessionService? sessionService = null)
    {
        return new CustomerAccountLifecycleService(
            dbContext,
            new StubPasswordHasher(),
            sessionService ?? new RecordingSessionService(),
            outbox,
            new CustomerAccountLifecycleOptions
            {
                EmailVerificationLifetimeMinutes = 10,
                PasswordResetLifetimeMinutes = 10,
                RefreshTokenLifetimeDays = 30,
                StorefrontBaseUrl = "https://storefront.example.test",
                CleanupIntervalHours = 24,
                ExpiredTokenRetentionDays = 7,
                LoginHistoryRetentionDays = 90
            },
            clock ?? TimeProvider.System,
            new RequestEmailVerificationRequestValidator(),
            new ConfirmEmailVerificationRequestValidator(),
            new ForgotPasswordRequestValidator(),
            new ResetPasswordRequestValidator());
    }

    private static Customer CreateCustomer(string email = "customer@example.com") => Customer.Create(
        Guid.NewGuid(),
        "Nguyen Van A",
        "0900000000",
        email,
        "hash:current-password");

    private static string ExtractToken(CustomerEmailMessage message)
    {
        var link = message.PlainTextBody.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
        return Uri.UnescapeDataString(new Uri(link).Query["?token=".Length..]);
    }

    private static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed class CapturingEmailOutbox : ICustomerEmailOutbox
    {
        public List<CustomerEmailMessage> Messages { get; } = [];

        public void Enqueue(CustomerEmailMessage message) => Messages.Add(message);
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class RecordingSessionService : ICustomerSessionService
    {
        public List<Guid> RevokedCustomerIds { get; } = [];

        public Task<CustomerAuthResponse> IssueAsync(Customer customer, CancellationToken cancellationToken = default) =>
            Task.FromException<CustomerAuthResponse>(new NotSupportedException());

        public Task<Result<CustomerAuthResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerAuthResponse>.Unauthorized("Not used."));

        public Task RevokeAsync(string refreshToken, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevokeAllAsync(Guid customerId, string reason, CancellationToken cancellationToken = default)
        {
            RevokedCustomerIds.Add(customerId);
            return Task.CompletedTask;
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
