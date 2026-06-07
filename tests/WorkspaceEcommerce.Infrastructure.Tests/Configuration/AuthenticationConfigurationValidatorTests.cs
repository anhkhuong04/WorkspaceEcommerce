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
