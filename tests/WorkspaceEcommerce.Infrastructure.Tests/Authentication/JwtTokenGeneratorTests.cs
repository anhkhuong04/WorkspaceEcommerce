using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WorkspaceEcommerce.Infrastructure.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Tests.Authentication;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateAdminToken_ReturnsBearerTokenWithAdminClaims()
    {
        var options = new JwtOptions
        {
            Issuer = "WorkspaceEcommerce",
            Audience = "WorkspaceEcommerce.Admin",
            SigningKey = "abcdefghijklmnopqrstuvwxyz1234567890",
            AccessTokenMinutes = 60
        };
        var generator = new JwtTokenGenerator(options);

        var response = generator.GenerateAdminToken("admin@example.com");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal("admin@example.com", response.Email);
        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(options.Issuer, token.Issuer);
        Assert.Contains(options.Audience, token.Audiences);
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == "admin@example.com");
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
    }

    [Fact]
    public void GenerateCustomerToken_ReturnsBearerTokenWithCustomerClaims()
    {
        var options = new JwtOptions
        {
            Issuer = "WorkspaceEcommerce",
            Audience = "WorkspaceEcommerce.Admin",
            SigningKey = "abcdefghijklmnopqrstuvwxyz1234567890",
            AccessTokenMinutes = 60
        };
        var customerId = Guid.NewGuid();
        var generator = new JwtTokenGenerator(options);

        var response = generator.GenerateCustomerToken(
            customerId,
            " CUSTOMER@EXAMPLE.COM ",
            " Nguyen Van A ",
            " 0900000000 ");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal("customer@example.com", response.Email);
        Assert.Equal("Nguyen Van A", response.FullName);
        Assert.Equal("0900000000", response.PhoneNumber);
        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(options.Issuer, token.Issuer);
        Assert.Contains(options.Audience, token.Audiences);
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == customerId.ToString("D"));
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == "customer@example.com");
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Customer");
    }
}
