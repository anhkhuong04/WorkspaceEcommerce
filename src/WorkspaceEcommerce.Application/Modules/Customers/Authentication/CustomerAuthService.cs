using FluentValidation;
using Google.Apis.Auth;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

internal sealed class CustomerAuthService(
    IAppDbContext dbContext,
    IValidator<CustomerRegisterRequest> registerValidator,
    IValidator<CustomerLoginRequest> loginValidator,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ICurrentCustomerContext currentCustomer) : ICustomerAuthService
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
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerAuthResponse>.Success(ToAuthResponse(customer));
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

        // Record login history if we have context
        if (customer is not null && !string.IsNullOrEmpty(request.IpAddress))
        {
            var rawUserAgent = string.IsNullOrWhiteSpace(request.UserAgent)
                ? "Unknown"
                : request.UserAgent.Trim();
            var userAgent = rawUserAgent[..Math.Min(rawUserAgent.Length, 499)];
            var loginHistory = new CustomerLoginHistory(
                Guid.NewGuid(),
                customer.Id,
                request.IpAddress,
                userAgent,
                success);
            dbContext.Add(loginHistory);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!success || customer is null)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid email or password.");
        }

        return Result<CustomerAuthResponse>.Success(ToAuthResponse(customer));
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

        var customer = dbContext.Customers.FirstOrDefault(c => c.Id == customerId.Value);
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
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<CustomerAuthResponse>> LoginWithGoogleAsync(
        CustomerGoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = string.IsNullOrWhiteSpace(request.GoogleClientId)
                    ? null
                    : [request.GoogleClientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            return Result<CustomerAuthResponse>.Unauthorized($"Google token is invalid: {ex.Message}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var googleId = payload.Subject;
        var email = NormalizeEmail(payload.Email);

        // Try to find existing customer by GoogleId or Email
        var customer = dbContext.Customers.FirstOrDefault(c => c.GoogleId == googleId)
                       ?? dbContext.Customers.FirstOrDefault(c => c.Email == email);

        if (customer is null)
        {
            // Create new customer from Google profile
            var fullName = payload.Name ?? payload.Email.Split('@')[0];
            customer = Customer.CreateFromGoogle(
                Guid.NewGuid(),
                fullName,
                email,
                googleId,
                avatarUrl: payload.Picture);

            dbContext.Add(customer);
        }
        else if (customer.GoogleId != googleId)
        {
            // Existing email/password account — link Google
            customer.LinkGoogleAccount(googleId);
            if (!string.IsNullOrEmpty(payload.Picture))
            {
                customer.UpdateAvatar(payload.Picture);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerAuthResponse>.Success(ToAuthResponse(customer));
    }

    private CustomerAuthResponse ToAuthResponse(Customer customer)
    {
        return tokenGenerator.GenerateCustomerToken(
            customer.Id,
            customer.Email,
            customer.FullName,
            customer.PhoneNumber);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
