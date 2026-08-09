using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
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
    public async Task QueueEmailVerificationAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        if (customer.IsEmailVerified)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        await InvalidateActiveTokensAsync(customer.Id, CustomerAccountTokenPurpose.EmailVerification, now, cancellationToken);
        var token = CreateToken(customer.Id, CustomerAccountTokenPurpose.EmailVerification, now);
        emailOutbox.Enqueue(new CustomerEmailMessage(
            customer.Email,
            "Verify your WorkspaceEcommerce email address",
            $"Verify your email address by opening: {BuildStorefrontLink("verify-email", token)}"));
    }

    public Task RevokeOutstandingPasswordResetTokensAsync(
        Guid customerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return InvalidateActiveTokensAsync(customerId, CustomerAccountTokenPurpose.PasswordReset, now, cancellationToken);
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

        var customer = await FindCustomerByEmailAsync(request.Email, cancellationToken);
        if (customer is not null && !customer.IsEmailVerified)
        {
            await QueueEmailVerificationAsync(customer, cancellationToken);
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
        var token = await FindActiveTokenAsync(request.Token, CustomerAccountTokenPurpose.EmailVerification, now, cancellationToken);
        if (token is null)
        {
            return Result.Unauthorized("Invalid or expired email verification link.");
        }

        var customer = await dbContext.Customers
            .Where(candidate => candidate.Id == token.CustomerId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
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

        var customer = await FindCustomerByEmailAsync(request.Email, cancellationToken);
        if (customer?.PasswordHash is not null)
        {
            var now = timeProvider.GetUtcNow();
            await InvalidateActiveTokensAsync(customer.Id, CustomerAccountTokenPurpose.PasswordReset, now, cancellationToken);
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
        var token = await FindActiveTokenAsync(request.Token, CustomerAccountTokenPurpose.PasswordReset, now, cancellationToken);
        if (token is null)
        {
            return Result.Unauthorized("Invalid or expired password reset link.");
        }

        var customer = await dbContext.Customers
            .Where(candidate => candidate.Id == token.CustomerId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (customer is null)
        {
            return Result.Unauthorized("Invalid or expired password reset link.");
        }

        token.Consume(now);
        await RevokeOutstandingPasswordResetTokensAsync(customer.Id, now, cancellationToken);
        customer.UpdatePasswordHash(passwordHasher.Hash(request.NewPassword));
        await sessionService.RevokeAllAsync(customer.Id, "password_reset", cancellationToken);
        return Result.Success();
    }

    private Task<Customer?> FindCustomerByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return dbContext.Customers
            .Where(candidate => candidate.Email == normalizedEmail)
            .FirstOrDefaultAsyncSafe(cancellationToken);
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

    private async Task<CustomerAccountToken?> FindActiveTokenAsync(
        string suppliedToken,
        CustomerAccountTokenPurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var token = await dbContext.CustomerAccountTokens
            .Where(candidate => candidate.Purpose == purpose && candidate.TokenHash == HashToken(suppliedToken))
            .FirstOrDefaultAsyncSafe(cancellationToken);
        return token is not null && token.IsActiveAt(now) ? token : null;
    }

    private async Task InvalidateActiveTokensAsync(
        Guid customerId,
        CustomerAccountTokenPurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var token in await dbContext.CustomerAccountTokens
                     .Where(candidate => candidate.CustomerId == customerId && candidate.Purpose == purpose && candidate.ConsumedAt == null)
                     .ToArrayAsyncSafe(cancellationToken))
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
