namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

/// <summary>Protects TOTP shared secrets before they are persisted.</summary>
public interface ITwoFactorSecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}
