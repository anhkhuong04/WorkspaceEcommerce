using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

internal sealed class CustomerAuthService(
    IAppDbContext dbContext,
    IValidator<CustomerRegisterRequest> registerValidator,
    IValidator<CustomerLoginRequest> loginValidator,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator) : ICustomerAuthService
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

        var customer = new Customer(
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
        if (customer is null || !passwordHasher.Verify(request.Password, customer.PasswordHash))
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid email or password.");
        }

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
