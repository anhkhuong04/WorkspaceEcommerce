using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

internal sealed class CustomerAccountLifecycleService(
    IAppDbContext dbContext,
    IPasswordHasher passwordHasher,
    ICustomerSessionService sessionService,
    ICustomerEmailOutbox emailOutbox,
    CustomerAccountLifecycleOptions options,
    TimeProvider timeProvider,
    IValidator<RequestEmailVerificationRequest> requestVerificationValidator,
    IValidator<ConfirmEmailVerificationRequest> confirmVerificationValidator,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator) : ICustomerAccountLifecycleService
{
    public void QueueEmailVerification(Customer customer)
    {
        if (customer.IsEmailVerified)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        InvalidateActiveTokens(customer.Id, CustomerAccountTokenPurpose.EmailVerification, now);
        var token = CreateToken(customer.Id, CustomerAccountTokenPurpose.EmailVerification, now);
        emailOutbox.Enqueue(new CustomerEmailMessage(
            customer.Email,
            "Verify your WorkspaceEcommerce email address",
            $"Verify your email address by opening: {BuildStorefrontLink("verify-email", token)}"));
    }

    public void RevokeOutstandingPasswordResetTokens(Guid customerId, DateTimeOffset now)
    {
        InvalidateActiveTokens(customerId, CustomerAccountTokenPurpose.PasswordReset, now);
    }

    public async Task<Result> RequestEmailVerificationAsync(
        RequestEmailVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await requestVerificationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var customer = FindCustomerByEmail(request.Email);
        if (customer is not null && !customer.IsEmailVerified)
        {
            QueueEmailVerification(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailVerificationAsync(
        ConfirmEmailVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await confirmVerificationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var now = timeProvider.GetUtcNow();
        var token = FindActiveToken(request.Token, CustomerAccountTokenPurpose.EmailVerification, now);
        if (token is null)
        {
            return Result.Unauthorized("Invalid or expired email verification link.");
        }

        var customer = dbContext.Customers.FirstOrDefault(candidate => candidate.Id == token.CustomerId);
        if (customer is null)
        {
            return Result.Unauthorized("Invalid or expired email verification link.");
        }

        token.Consume(now);
        customer.MarkEmailVerified();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await forgotPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var customer = FindCustomerByEmail(request.Email);
        if (customer?.PasswordHash is not null)
        {
            var now = timeProvider.GetUtcNow();
            InvalidateActiveTokens(customer.Id, CustomerAccountTokenPurpose.PasswordReset, now);
            var token = CreateToken(customer.Id, CustomerAccountTokenPurpose.PasswordReset, now);
            emailOutbox.Enqueue(new CustomerEmailMessage(
                customer.Email,
                "Reset your WorkspaceEcommerce password",
                $"Reset your password by opening: {BuildStorefrontLink("reset-password", token)}"));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var now = timeProvider.GetUtcNow();
        var token = FindActiveToken(request.Token, CustomerAccountTokenPurpose.PasswordReset, now);
        if (token is null)
        {
            return Result.Unauthorized("Invalid or expired password reset link.");
        }

        var customer = dbContext.Customers.FirstOrDefault(candidate => candidate.Id == token.CustomerId);
        if (customer is null)
        {
            return Result.Unauthorized("Invalid or expired password reset link.");
        }

        token.Consume(now);
        RevokeOutstandingPasswordResetTokens(customer.Id, now);
        customer.UpdatePasswordHash(passwordHasher.Hash(request.NewPassword));
        await sessionService.RevokeAllAsync(customer.Id, "password_reset", cancellationToken);
        return Result.Success();
    }

    private Customer? FindCustomerByEmail(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return dbContext.Customers.FirstOrDefault(candidate => candidate.Email == normalizedEmail);
    }

    private string CreateToken(Guid customerId, CustomerAccountTokenPurpose purpose, DateTimeOffset now)
    {
        var token = GenerateToken();
        var lifetime = purpose == CustomerAccountTokenPurpose.EmailVerification
            ? options.EmailVerificationLifetimeMinutes
            : options.PasswordResetLifetimeMinutes;
        dbContext.Add(new CustomerAccountToken(
            Guid.NewGuid(),
            customerId,
            purpose,
            HashToken(token),
            now,
            now.AddMinutes(lifetime)));
        return token;
    }

    private CustomerAccountToken? FindActiveToken(
        string suppliedToken,
        CustomerAccountTokenPurpose purpose,
        DateTimeOffset now)
    {
        var token = dbContext.CustomerAccountTokens.FirstOrDefault(candidate =>
            candidate.Purpose == purpose && candidate.TokenHash == HashToken(suppliedToken));
        return token is not null && token.IsActiveAt(now) ? token : null;
    }

    private void InvalidateActiveTokens(Guid customerId, CustomerAccountTokenPurpose purpose, DateTimeOffset now)
    {
        foreach (var token in dbContext.CustomerAccountTokens
                     .Where(candidate => candidate.CustomerId == customerId && candidate.Purpose == purpose && candidate.ConsumedAt == null)
                     .ToArray())
        {
            token.Consume(now);
        }
    }

    private static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string BuildStorefrontLink(string path, string token)
    {
        return $"{options.StorefrontBaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";
    }
}
