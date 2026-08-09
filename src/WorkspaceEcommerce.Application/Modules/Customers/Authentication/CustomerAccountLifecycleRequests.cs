namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

public sealed record RequestEmailVerificationRequest(string Email);

public sealed record ConfirmEmailVerificationRequest(string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);
