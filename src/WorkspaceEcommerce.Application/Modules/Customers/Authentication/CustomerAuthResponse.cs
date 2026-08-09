using System.Text.Json.Serialization;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed record CustomerAuthResponse(
    string? AccessToken,
    string? TokenType,
    DateTimeOffset? ExpiresAt,
    Guid CustomerId,
    string Email,
    string FullName,
    string PhoneNumber,
    bool RequiresTwoFactor = false,
    string? TwoFactorChallengeToken = null)
{
    /// <summary>
    /// Transient server-only value copied to an HttpOnly cookie by the API
    /// controller. It is intentionally excluded from JSON response bodies.
    /// </summary>
    [JsonIgnore]
    public string? RefreshToken { get; init; }

    public static CustomerAuthResponse TwoFactorRequired(
        Guid customerId,
        string email,
        string fullName,
        string phoneNumber,
        string challengeToken)
    {
        return new CustomerAuthResponse(
            null,
            null,
            null,
            customerId,
            email,
            fullName,
            phoneNumber,
            true,
            challengeToken);
    }
}
