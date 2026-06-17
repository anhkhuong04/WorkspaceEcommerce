using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

internal sealed class CustomerProfileService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    IValidator<UpdateCustomerProfileRequest> updateValidator) : ICustomerProfileService
{
    public Task<Result<CustomerProfileDto>> GetMeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Task.FromResult(Result<CustomerProfileDto>.Unauthorized("Customer authentication is required."));
        }

        var customer = FindCustomerById(customerId.Value);
        return customer is null
            ? Task.FromResult(Result<CustomerProfileDto>.NotFound("Customer was not found."))
            : Task.FromResult(Result<CustomerProfileDto>.Success(ToDto(customer)));
    }

    public async Task<Result<CustomerProfileDto>> UpdateMeAsync(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CustomerProfileDto>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerProfileDto>.Unauthorized("Customer authentication is required.");
        }

        var customer = FindCustomerById(customerId.Value);
        if (customer is null)
        {
            return Result<CustomerProfileDto>.NotFound("Customer was not found.");
        }

        customer.UpdateProfile(request.FullName, request.PhoneNumber);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerProfileDto>.Success(ToDto(customer));
    }

    private Customer? FindCustomerById(Guid customerId)
    {
        return dbContext.Customers.FirstOrDefault(customer => customer.Id == customerId);
    }

    private static CustomerProfileDto ToDto(Customer customer)
    {
        return new CustomerProfileDto(
            customer.Id,
            customer.FullName,
            customer.PhoneNumber,
            customer.Email,
            customer.CreatedAt,
            customer.UpdatedAt);
    }
}
