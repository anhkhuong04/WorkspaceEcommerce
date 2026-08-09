using Microsoft.AspNetCore.DataProtection;
using WorkspaceEcommerce.Application.Abstractions.Authentication;

namespace WorkspaceEcommerce.Infrastructure.Authentication;

internal sealed class DataProtectionTwoFactorSecretProtector(IDataProtectionProvider dataProtectionProvider)
    : ITwoFactorSecretProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider
        .CreateProtector("WorkspaceEcommerce.Customer.TwoFactorSecret.v1");

    public string Protect(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return _protector.Protect(secret);
    }

    public string Unprotect(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        return _protector.Unprotect(protectedSecret);
    }
}
