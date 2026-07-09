namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

/// <summary>Request to authenticate a customer using a Google ID token.</summary>
public sealed record CustomerGoogleLoginRequest(
    /// <summary>The Google ID token credential returned by the Google Sign-In SDK.</summary>
    string IdToken,
    /// <summary>The Google OAuth Client ID used for audience validation. Optional in development.</summary>
    string? GoogleClientId = null);
