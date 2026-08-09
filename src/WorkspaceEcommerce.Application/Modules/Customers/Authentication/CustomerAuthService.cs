using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

internal sealed class CustomerAuthService(
    IAppDbContext dbContext,
    IValidator<CustomerRegisterRequest> registerValidator,
    IValidator<CustomerLoginRequest> loginValidator,
    IPasswordHasher passwordHasher,
    ICurrentCustomerContext currentCustomer,
    IGoogleIdTokenValidator googleIdTokenValidator,
    ICustomerTwoFactorService twoFactorService,
    ICustomerSessionService sessionService,
    ICustomerAccountLifecycleService accountLifecycleService) : ICustomerAuthService
{
    public async Task<Result<CustomerAuthResponse>> RegisterAsync(
        CustomerRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CustomerAuthResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var email = NormalizeEmail(request.Email);
        if (dbContext.Customers.Any(customer => customer.Email == email))
        {
            return Result<CustomerAuthResponse>.Conflict("Customer email is already registered.");
        }

        var customer = Customer.Create(
            Guid.NewGuid(),
            request.FullName,
            request.PhoneNumber,
            email,
            passwordHasher.Hash(request.Password));

        dbContext.Add(customer);
        accountLifecycleService.QueueEmailVerification(customer);

        return Result<CustomerAuthResponse>.Success(await sessionService.IssueAsync(customer, cancellationToken));
    }

    public async Task<Result<CustomerAuthResponse>> LoginAsync(
        CustomerLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CustomerAuthResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var email = NormalizeEmail(request.Email);
        var customer = dbContext.Customers.FirstOrDefault(existing => existing.Email == email);
        var success = customer is not null
            && customer.PasswordHash is not null
            && passwordHasher.Verify(request.Password, customer.PasswordHash);

        if (customer is not null && !string.IsNullOrEmpty(request.IpAddress))
        {
            var rawUserAgent = string.IsNullOrWhiteSpace(request.UserAgent)
                ? "Unknown"
                : request.UserAgent.Trim();
            var userAgent = rawUserAgent[..Math.Min(rawUserAgent.Length, 499)];
            dbContext.Add(new CustomerLoginHistory(
                Guid.NewGuid(),
                customer.Id,
                request.IpAddress,
                userAgent,
                success));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!success || customer is null)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid email or password.");
        }

        var twoFactorChallenge = await twoFactorService.CreateLoginChallengeAsync(customer, cancellationToken);
        return twoFactorChallenge is not null
            ? Result<CustomerAuthResponse>.Success(twoFactorChallenge)
            : Result<CustomerAuthResponse>.Success(await sessionService.IssueAsync(customer, cancellationToken));
    }

    public async Task<Result> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result.Unauthorized("Customer authentication is required.");
        }

        var customer = dbContext.Customers.FirstOrDefault(candidate => candidate.Id == customerId.Value);
        if (customer is null)
        {
            return Result.NotFound("Customer was not found.");
        }

        if (customer.PasswordHash is null || !passwordHasher.Verify(request.CurrentPassword, customer.PasswordHash))
        {
            return Result.Unauthorized("Current password is incorrect.");
        }

        if (request.NewPassword.Length < 8)
        {
            return Result.Validation(["New password must be at least 8 characters."]);
        }

        customer.UpdatePasswordHash(passwordHasher.Hash(request.NewPassword));
        var now = DateTimeOffset.UtcNow;
        accountLifecycleService.RevokeOutstandingPasswordResetTokens(customer.Id, now);
        await sessionService.RevokeAllAsync(customer.Id, "password_changed", cancellationToken);

        return Result.Success();
    }

    public async Task<Result<CustomerAuthResponse>> LoginWithGoogleAsync(
        CustomerGoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = await googleIdTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Google authentication failed.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var googleId = identity.Subject.Trim();
        var email = NormalizeEmail(identity.Email);
        var customerByGoogleId = dbContext.Customers.FirstOrDefault(customer => customer.GoogleId == googleId);
        var customerByEmail = dbContext.Customers.FirstOrDefault(customer => customer.Email == email);

        if (customerByGoogleId is not null &&
            customerByEmail is not null &&
            customerByGoogleId.Id != customerByEmail.Id)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Google authentication failed.");
        }

        var customer = customerByGoogleId ?? customerByEmail;
        if (customer is null)
        {
            var fullName = string.IsNullOrWhiteSpace(identity.Name)
                ? email.Split('@')[0]
                : identity.Name.Trim();
            customer = Customer.CreateFromGoogle(
                Guid.NewGuid(),
                fullName,
                email,
                googleId,
                identity.Picture);
            dbContext.Add(customer);
        }
        else if (customerByGoogleId is null)
        {
            if (!string.IsNullOrWhiteSpace(customer.GoogleId))
            {
                return Result<CustomerAuthResponse>.Unauthorized("Google authentication failed.");
            }

            customer.LinkGoogleAccount(googleId);
            if (!string.IsNullOrWhiteSpace(identity.Picture))
            {
                customer.UpdateAvatar(identity.Picture);
            }
        }

        var twoFactorChallenge = await twoFactorService.CreateLoginChallengeAsync(customer, cancellationToken);
        return twoFactorChallenge is not null
            ? Result<CustomerAuthResponse>.Success(twoFactorChallenge)
            : Result<CustomerAuthResponse>.Success(await sessionService.IssueAsync(customer, cancellationToken));
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
