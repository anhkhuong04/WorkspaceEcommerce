using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Authentication;

internal sealed class JwtTokenGenerator(JwtOptions options) : IJwtTokenGenerator
{
    public AdminLoginResponse GenerateAdminToken(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var accessToken = CreateToken(
            expiresAt,
            [
                new Claim(JwtRegisteredClaimNames.Sub, normalizedEmail),
                new Claim(JwtRegisteredClaimNames.Email, normalizedEmail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(ClaimTypes.Name, normalizedEmail),
                new Claim(ClaimTypes.Role, AuthRoles.Admin)
            ]);

        return new AdminLoginResponse(
            accessToken,
            "Bearer",
            expiresAt,
            normalizedEmail);
    }

    public CustomerAuthResponse GenerateCustomerToken(
        Guid customerId,
        string email,
        string fullName,
        string phoneNumber)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedFullName = fullName.Trim();
        var normalizedPhoneNumber = phoneNumber.Trim();
        var customerIdValue = customerId.ToString("D");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var accessToken = CreateToken(
            expiresAt,
            [
                new Claim(JwtRegisteredClaimNames.Sub, customerIdValue),
                new Claim(ClaimTypes.NameIdentifier, customerIdValue),
                new Claim("customer_id", customerIdValue),
                new Claim(JwtRegisteredClaimNames.Email, normalizedEmail),
                new Claim(ClaimTypes.Email, normalizedEmail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(ClaimTypes.Name, normalizedFullName),
                new Claim(ClaimTypes.Role, AuthRoles.Customer)
            ]);

        return new CustomerAuthResponse(
            accessToken,
            "Bearer",
            expiresAt,
            customerId,
            normalizedEmail,
            normalizedFullName,
            normalizedPhoneNumber);
    }

    private string CreateToken(DateTimeOffset expiresAt, IEnumerable<Claim> claims)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
