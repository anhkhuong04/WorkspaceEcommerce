using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Customers.Profile;

internal sealed class CustomerProfileService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    IValidator<UpdateCustomerProfileRequest> updateValidator) : ICustomerProfileService
{
    public async Task<Result<CustomerProfileDto>> GetMeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerProfileDto>.Unauthorized("Customer authentication is required.");
        }

        var customer = await FindCustomerByIdAsync(customerId.Value, cancellationToken);
        return customer is null
            ? Result<CustomerProfileDto>.NotFound("Customer was not found.")
            : Result<CustomerProfileDto>.Success(ToDto(customer));
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

        var customer = await FindCustomerByIdAsync(customerId.Value, cancellationToken);
        if (customer is null)
        {
            return Result<CustomerProfileDto>.NotFound("Customer was not found.");
        }

        customer.UpdateProfile(request.FullName, request.PhoneNumber);
        customer.UpdateAvatar(request.AvatarUrl);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerProfileDto>.Success(ToDto(customer));
    }

    public async Task<Result<CustomerAccountStatsDto>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerAccountStatsDto>.Unauthorized("Customer authentication is required.");
        }

        var customer = await FindCustomerByIdAsync(customerId.Value, cancellationToken);
        if (customer is null)
        {
            return Result<CustomerAccountStatsDto>.NotFound("Customer was not found.");
        }

        var orderStats = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(order => order.CustomerId == customerId.Value)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Pending = group.Count(order => order.Status == OrderStatus.Pending || order.Status == OrderStatus.Confirmed),
                Shipping = group.Count(order => order.Status == OrderStatus.Processing || order.Status == OrderStatus.Shipping)
            })
            .FirstOrDefaultAsyncSafe(cancellationToken);

        var stats = new CustomerAccountStatsDto(
            orderStats?.Total ?? 0,
            orderStats?.Pending ?? 0,
            orderStats?.Shipping ?? 0,
            customer.RewardPoints);

        return Result<CustomerAccountStatsDto>.Success(stats);
    }

    public async Task<Result<IReadOnlyList<CustomerLoginHistoryDto>>> GetLoginHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<IReadOnlyList<CustomerLoginHistoryDto>>.Unauthorized("Customer authentication is required.");
        }

        var history = await dbContext.CustomerLoginHistories
            .AsNoTrackingIfEf()
            .Where(h => h.CustomerId == customerId.Value)
            .OrderByDescending(h => h.LoginTime)
            .ThenByDescending(h => h.Id)
            .Take(20)
            .Select(h => new CustomerLoginHistoryDto(h.Id, h.LoginTime, h.IpAddress, h.UserAgent, h.Success))
            .ToArrayAsyncSafe(cancellationToken);

        return Result<IReadOnlyList<CustomerLoginHistoryDto>>.Success(history);
    }

    private Task<Customer?> FindCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .Where(customer => customer.Id == customerId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
    }

    private static CustomerProfileDto ToDto(Customer customer)
    {
        return new CustomerProfileDto(
            customer.Id,
            customer.FullName,
            customer.PhoneNumber,
            customer.Email,
            customer.AvatarUrl,
            customer.IsEmailVerified,
            customer.RewardPoints,
            customer.TwoFactorEnabled,
            customer.CreatedAt,
            customer.UpdatedAt);
    }
}
