using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Authentication;

internal sealed class GoogleIdTokenValidator(
    GoogleAuthOptions options,
    IGoogleJwtValidator? googleJwtValidator = null) : IGoogleIdTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var payload = await (googleJwtValidator ?? new GoogleApiJwtValidator()).ValidateAsync(
                idToken.Trim(),
                options.AllowedClientIds,
                cancellationToken);

            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.Subject) ||
                string.IsNullOrWhiteSpace(payload.Email) ||
                !payload.EmailVerified)
            {
                return null;
            }

            return new GoogleIdentity(
                payload.Subject,
                payload.Email,
                payload.Name,
                payload.Picture);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
