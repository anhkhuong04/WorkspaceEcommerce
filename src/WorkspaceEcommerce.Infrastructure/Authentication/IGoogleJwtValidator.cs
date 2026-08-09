using Google.Apis.Auth;

namespace WorkspaceEcommerce.Infrastructure.Authentication;

internal interface IGoogleJwtValidator
{
    Task<GoogleJwtIdentity?> ValidateAsync(
        string idToken,
        IReadOnlyCollection<string> allowedClientIds,
        CancellationToken cancellationToken = default);
}

internal sealed record GoogleJwtIdentity(
    string? Subject,
    string? Email,
    bool EmailVerified,
    string? Name,
    string? Picture);

internal sealed class GoogleApiJwtValidator : IGoogleJwtValidator
{
    public async Task<GoogleJwtIdentity?> ValidateAsync(
        string idToken,
        IReadOnlyCollection<string> allowedClientIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = allowedClientIds.ToArray()
            });

        return new GoogleJwtIdentity(
            payload.Subject,
            payload.Email,
            payload.EmailVerified,
            payload.Name,
            payload.Picture);
    }
}
