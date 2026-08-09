using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.TwoFactor;

internal sealed class CustomerTwoFactorService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    ITwoFactorSecretProtector secretProtector,
    TwoFactorOptions options,
    TimeProvider timeProvider,
    IValidator<ConfirmTwoFactorSetupRequest> confirmSetupValidator,
    IValidator<DisableTwoFactorRequest> disableValidator,
    IValidator<VerifyTwoFactorLoginRequest> verifyLoginValidator,
    IValidator<VerifyTwoFactorRecoveryRequest> verifyRecoveryValidator,
    ICustomerSessionService sessionService) : ICustomerTwoFactorService
{
    public async Task<Result<TwoFactorSetupStartResponse>> StartSetupAsync(
        CancellationToken cancellationToken = default)
    {
        var customerResult = FindCurrentCustomer();
        if (!customerResult.IsSuccess)
        {
            return ToTyped<TwoFactorSetupStartResponse>(customerResult);
        }

        var customer = customerResult.Value!;
        if (customer.TwoFactorEnabled)
        {
            return Result<TwoFactorSetupStartResponse>.Conflict("Two-factor authentication is already enabled.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.SetupLifetimeMinutes);
        var secret = totpService.GenerateSecret();
        customer.BeginTwoFactorSetup(secretProtector.Protect(secret), expiresAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TwoFactorSetupStartResponse>.Success(
            new TwoFactorSetupStartResponse(
                secret,
                totpService.CreateProvisioningUri(secret, options.Issuer, customer.Email),
                expiresAt));
    }

    public async Task<Result<TwoFactorSetupConfirmationResponse>> ConfirmSetupAsync(
        ConfirmTwoFactorSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await confirmSetupValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<TwoFactorSetupConfirmationResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var customerResult = FindCurrentCustomer();
        if (!customerResult.IsSuccess)
        {
            return ToTyped<TwoFactorSetupConfirmationResponse>(customerResult);
        }

        var customer = customerResult.Value!;
        var now = timeProvider.GetUtcNow();
        if (customer.TwoFactorEnabled ||
            string.IsNullOrWhiteSpace(customer.PendingTwoFactorSecret) ||
            !customer.TwoFactorSetupExpiresAt.HasValue ||
            customer.TwoFactorSetupExpiresAt <= now)
        {
            if (!customer.TwoFactorEnabled && customer.PendingTwoFactorSecret is not null)
            {
                customer.CancelPendingTwoFactorSetup();
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result<TwoFactorSetupConfirmationResponse>.Validation(
                ["A pending two-factor setup is required. Start setup again."]);
        }

        if (!TryVerifyTotp(customer.PendingTwoFactorSecret, request.Code, now, out _))
        {
            return Result<TwoFactorSetupConfirmationResponse>.Unauthorized("Invalid authentication code.");
        }

        customer.ConfirmTwoFactorSetup();
        foreach (var existingCode in dbContext.CustomerTwoFactorRecoveryCodes
                     .Where(code => code.CustomerId == customer.Id)
                     .ToArray())
        {
            dbContext.Remove(existingCode);
        }

        var recoveryCodes = GenerateRecoveryCodes(options.RecoveryCodeCount);
        foreach (var recoveryCode in recoveryCodes)
        {
            dbContext.Add(new CustomerTwoFactorRecoveryCode(
                Guid.NewGuid(),
                customer.Id,
                passwordHasher.Hash(recoveryCode),
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TwoFactorSetupConfirmationResponse>.Success(
            new TwoFactorSetupConfirmationResponse(recoveryCodes));
    }

    public async Task<Result> DisableAsync(
        DisableTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await disableValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var customerResult = FindCurrentCustomer();
        if (!customerResult.IsSuccess)
        {
            return ToUntyped(customerResult);
        }

        var customer = customerResult.Value!;
        if (!customer.TwoFactorEnabled || string.IsNullOrWhiteSpace(customer.TwoFactorSecret))
        {
            return Result.Conflict("Two-factor authentication is not enabled.");
        }

        var now = timeProvider.GetUtcNow();
        var verified = false;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            verified = TryVerifyTotp(customer.TwoFactorSecret, request.Code, now, out var timeStep) &&
                       customer.TryUseTwoFactorTimeStep(timeStep);
        }
        else if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            var recoveryCode = FindUnusedRecoveryCode(customer.Id, request.RecoveryCode);
            if (recoveryCode is not null)
            {
                recoveryCode.MarkUsed(now);
                verified = true;
            }
        }

        if (!verified)
        {
            return Result.Unauthorized("Invalid two-factor authentication proof.");
        }

        customer.DisableTwoFactor();
        foreach (var recoveryCode in dbContext.CustomerTwoFactorRecoveryCodes
                     .Where(code => code.CustomerId == customer.Id)
                     .ToArray())
        {
            dbContext.Remove(recoveryCode);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConcurrencyException)
        {
            return Result.Unauthorized("Invalid two-factor authentication proof.");
        }

        return Result.Success();
    }

    public async Task<CustomerAuthResponse?> CreateLoginChallengeAsync(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        if (!customer.TwoFactorEnabled || string.IsNullOrWhiteSpace(customer.TwoFactorSecret))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var previousChallenge in dbContext.CustomerTwoFactorChallenges
                     .Where(challenge => challenge.CustomerId == customer.Id && challenge.ConsumedAt == null)
                     .ToArray())
        {
            dbContext.Remove(previousChallenge);
        }

        var token = GenerateChallengeToken();
        dbContext.Add(new CustomerTwoFactorChallenge(
            Guid.NewGuid(),
            customer.Id,
            HashChallengeToken(token),
            now.AddMinutes(options.ChallengeLifetimeMinutes),
            now));
        await dbContext.SaveChangesAsync(cancellationToken);

        return CustomerAuthResponse.TwoFactorRequired(
            customer.Id,
            customer.Email,
            customer.FullName,
            customer.PhoneNumber ?? string.Empty,
            token);
    }

    public async Task<Result<CustomerAuthResponse>> VerifyLoginAsync(
        VerifyTwoFactorLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await verifyLoginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CustomerAuthResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var now = timeProvider.GetUtcNow();
        var challenge = FindActiveChallenge(request.ChallengeToken, now);
        if (challenge is null)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid or expired two-factor challenge.");
        }

        var customer = dbContext.Customers.FirstOrDefault(candidate => candidate.Id == challenge.CustomerId);
        if (customer is null || !customer.TwoFactorEnabled || string.IsNullOrWhiteSpace(customer.TwoFactorSecret) ||
            !TryVerifyTotp(customer.TwoFactorSecret, request.Code, now, out var timeStep) ||
            !customer.TryUseTwoFactorTimeStep(timeStep))
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid authentication code.");
        }

        challenge.Consume(now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConcurrencyException)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid authentication code.");
        }

        return Result<CustomerAuthResponse>.Success(await sessionService.IssueAsync(customer, cancellationToken));
    }

    public async Task<Result<CustomerAuthResponse>> VerifyRecoveryAsync(
        VerifyTwoFactorRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await verifyRecoveryValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CustomerAuthResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var now = timeProvider.GetUtcNow();
        var challenge = FindActiveChallenge(request.ChallengeToken, now);
        if (challenge is null)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid or expired two-factor challenge.");
        }

        var customer = dbContext.Customers.FirstOrDefault(candidate => candidate.Id == challenge.CustomerId);
        var recoveryCode = customer is null || !customer.TwoFactorEnabled
            ? null
            : FindUnusedRecoveryCode(customer.Id, request.RecoveryCode);
        if (recoveryCode is null || customer is null)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid recovery code.");
        }

        recoveryCode.MarkUsed(now);
        challenge.Consume(now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConcurrencyException)
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid recovery code.");
        }

        return Result<CustomerAuthResponse>.Success(await sessionService.IssueAsync(customer, cancellationToken));
    }

    private Result<Customer> FindCurrentCustomer()
    {
        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<Customer>.Unauthorized("Customer authentication is required.");
        }

        var customer = dbContext.Customers.FirstOrDefault(candidate => candidate.Id == customerId.Value);
        return customer is null
            ? Result<Customer>.NotFound("Customer was not found.")
            : Result<Customer>.Success(customer);
    }

    private static Result<T> ToTyped<T>(Result result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<T>.Validation(result.Errors),
            ResultStatus.Unauthorized => Result<T>.Unauthorized(result.FirstError ?? "Customer authentication is required."),
            ResultStatus.NotFound => Result<T>.NotFound(result.FirstError ?? "The requested resource was not found."),
            ResultStatus.Conflict => Result<T>.Conflict(result.FirstError ?? "A conflict occurred."),
            _ => Result<T>.Failure(result.Errors)
        };
    }

    private static Result ToUntyped(Result result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result.Validation(result.Errors),
            ResultStatus.Unauthorized => Result.Unauthorized(result.FirstError ?? "Customer authentication is required."),
            ResultStatus.NotFound => Result.NotFound(result.FirstError ?? "The requested resource was not found."),
            ResultStatus.Conflict => Result.Conflict(result.FirstError ?? "A conflict occurred."),
            _ => Result.Failure(result.Errors)
        };
    }

    private CustomerTwoFactorChallenge? FindActiveChallenge(string token, DateTimeOffset now)
    {
        var tokenHash = HashChallengeToken(token);
        var challenge = dbContext.CustomerTwoFactorChallenges
            .FirstOrDefault(candidate => candidate.TokenHash == tokenHash);

        return challenge is not null && challenge.IsActiveAt(now)
            ? challenge
            : null;
    }

    private CustomerTwoFactorRecoveryCode? FindUnusedRecoveryCode(Guid customerId, string suppliedCode)
    {
        foreach (var recoveryCode in dbContext.CustomerTwoFactorRecoveryCodes
                     .Where(code => code.CustomerId == customerId && code.UsedAt == null)
                     .ToArray())
        {
            if (passwordHasher.Verify(suppliedCode.Trim(), recoveryCode.CodeHash))
            {
                return recoveryCode;
            }
        }

        return null;
    }

    private bool TryVerifyTotp(string protectedSecret, string code, DateTimeOffset now, out long timeStep)
    {
        timeStep = 0;
        try
        {
            return totpService.TryVerifyCode(secretProtector.Unprotect(protectedSecret), code, now, out timeStep);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string GenerateChallengeToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashChallengeToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static IReadOnlyList<string> GenerateRecoveryCodes(int count)
    {
        var codes = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            codes.Add(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        }

        return codes;
    }
}
