using WorkspaceEcommerce.Infrastructure.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Tests.Authentication;

public sealed class GoogleIdTokenValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenIdentityIsValidForServerAllowlist_ReturnsIdentity()
    {
        var jwtValidator = new StubGoogleJwtValidator(
            new GoogleJwtIdentity("google-subject", "customer@example.com", true, "Customer", null));
        var validator = new GoogleIdTokenValidator(
            new GoogleAuthOptions
            {
                Enabled = true,
                AllowedClientIds = ["storefront-client.apps.googleusercontent.com"]
            },
            jwtValidator);

        var result = await validator.ValidateAsync("id-token");

        Assert.NotNull(result);
        Assert.Equal("google-subject", result.Subject);
        Assert.Equal("customer@example.com", result.Email);
        Assert.Equal(["storefront-client.apps.googleusercontent.com"], jwtValidator.ReceivedAllowedClientIds);
    }

    [Fact]
    public async Task ValidateAsync_WhenProviderRejectsAnotherAudience_ReturnsNull()
    {
        var jwtValidator = new StubGoogleJwtValidator(new InvalidOperationException("audience mismatch"));
        var validator = new GoogleIdTokenValidator(
            new GoogleAuthOptions
            {
                Enabled = true,
                AllowedClientIds = ["storefront-client.apps.googleusercontent.com"]
            },
            jwtValidator);

        var result = await validator.ValidateAsync("id-token-for-other-audience");

        Assert.Null(result);
        Assert.Equal(["storefront-client.apps.googleusercontent.com"], jwtValidator.ReceivedAllowedClientIds);
    }

    [Theory]
    [InlineData(null, "customer@example.com", true)]
    [InlineData("google-subject", "customer@example.com", false)]
    [InlineData("google-subject", null, true)]
    public async Task ValidateAsync_WhenRequiredClaimsAreMissingOrUnverified_ReturnsNull(
        string? subject,
        string? email,
        bool emailVerified)
    {
        var validator = new GoogleIdTokenValidator(
            new GoogleAuthOptions
            {
                Enabled = true,
                AllowedClientIds = ["storefront-client.apps.googleusercontent.com"]
            },
            new StubGoogleJwtValidator(new GoogleJwtIdentity(subject, email, emailVerified, null, null)));

        var result = await validator.ValidateAsync("id-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_WhenGoogleLoginIsDisabled_DoesNotValidateToken()
    {
        var jwtValidator = new StubGoogleJwtValidator(
            new GoogleJwtIdentity("google-subject", "customer@example.com", true, null, null));
        var validator = new GoogleIdTokenValidator(
            new GoogleAuthOptions { Enabled = false },
            jwtValidator);

        var result = await validator.ValidateAsync("id-token");

        Assert.Null(result);
        Assert.Null(jwtValidator.ReceivedAllowedClientIds);
    }

    private sealed class StubGoogleJwtValidator : IGoogleJwtValidator
    {
        private readonly GoogleJwtIdentity? _identity;
        private readonly Exception? _exception;

        public StubGoogleJwtValidator(GoogleJwtIdentity identity)
        {
            _identity = identity;
        }

        public StubGoogleJwtValidator(Exception exception)
        {
            _exception = exception;
        }

        public IReadOnlyCollection<string>? ReceivedAllowedClientIds { get; private set; }

        public Task<GoogleJwtIdentity?> ValidateAsync(
            string idToken,
            IReadOnlyCollection<string> allowedClientIds,
            CancellationToken cancellationToken = default)
        {
            ReceivedAllowedClientIds = allowedClientIds.ToArray();
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_identity);
        }
    }
}
