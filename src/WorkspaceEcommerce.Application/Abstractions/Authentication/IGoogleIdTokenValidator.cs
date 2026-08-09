namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

/// <summary>Validates a Google ID token using only server-owned trust configuration.</summary>
public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record GoogleIdentity(
    string Subject,
    string Email,
    string? Name,
    string? Picture);
