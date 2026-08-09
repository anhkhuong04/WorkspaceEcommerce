using Microsoft.Extensions.Configuration;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Tests.Configuration;

public sealed class AuthenticationConfigurationValidatorTests
{
    [Fact]
    public void GetValidatedJwtOptions_WhenConfigurationIsValid_ReturnsOptions()
    {
        var configuration = BuildConfiguration();

        var options = configuration.GetValidatedJwtOptions();

        Assert.Equal("WorkspaceEcommerce", options.Issuer);
        Assert.Equal("WorkspaceEcommerce.Admin", options.Audience);
        Assert.Equal(60, options.AccessTokenMinutes);
    }

    [Fact]
    public void GetValidatedJwtOptions_WhenSigningKeyIsPlaceholder_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "CHANGE_ME"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedJwtOptions);

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedJwtOptions_WhenSigningKeyIsTooShort_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "short"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedJwtOptions);

        Assert.Contains("at least 32 bytes", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedAdminAuthOptions_WhenConfigurationIsValid_ReturnsOptions()
    {
        var configuration = BuildConfiguration();

        var options = configuration.GetValidatedAdminAuthOptions();

        Assert.Equal("admin@example.com", options.Email);
        Assert.Equal("local-dev-password", options.Password);
    }

    [Fact]
    public void GetValidatedAdminAuthOptions_WhenPasswordIsPlaceholder_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AdminAuth:Password"] = "CHANGE_ME"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedAdminAuthOptions);

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedAdminAuthOptions_WhenEmailIsInvalid_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AdminAuth:Email"] = "not-an-email"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedAdminAuthOptions);

        Assert.Contains("valid email", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedGoogleAuthOptions_WhenEnabledWithoutClientIds_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["GoogleAuth:Enabled"] = "true"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedGoogleAuthOptions);

        Assert.Contains("client ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedGoogleAuthOptions_WhenEnabledWithAllowlist_ReturnsServerOwnedIds()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["GoogleAuth:Enabled"] = "true",
            ["GoogleAuth:AllowedClientIds:0"] = "storefront-client.apps.googleusercontent.com",
            ["GoogleAuth:AllowedClientIds:1"] = "staging-client.apps.googleusercontent.com"
        });

        var options = configuration.GetValidatedGoogleAuthOptions();

        Assert.True(options.Enabled);
        Assert.Equal(
            ["storefront-client.apps.googleusercontent.com", "staging-client.apps.googleusercontent.com"],
            options.AllowedClientIds);
    }

    [Fact]
    public void GetValidatedGoogleAuthOptions_WhenEnabledWithPlaceholder_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["GoogleAuth:Enabled"] = "true",
            ["GoogleAuth:AllowedClientIds:0"] = "CHANGE_ME_GOOGLE_OAUTH_CLIENT_ID"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedGoogleAuthOptions);

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidatedTwoFactorOptions_WhenChallengeLifetimeIsTooLong_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["TwoFactor:ChallengeLifetimeMinutes"] = "16"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetValidatedTwoFactorOptions);

        Assert.Contains("ChallengeLifetimeMinutes", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["AdminAuth:Email"] = "admin@example.com",
            ["AdminAuth:Password"] = "local-dev-password",
            ["Jwt:Issuer"] = "WorkspaceEcommerce",
            ["Jwt:Audience"] = "WorkspaceEcommerce.Admin",
            ["Jwt:SigningKey"] = "abcdefghijklmnopqrstuvwxyz1234567890",
            ["Jwt:AccessTokenMinutes"] = "60"
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
