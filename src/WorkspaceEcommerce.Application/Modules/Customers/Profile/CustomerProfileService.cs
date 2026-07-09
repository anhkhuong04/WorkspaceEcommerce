using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;

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
        customer.UpdateAvatar(request.AvatarUrl);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerProfileDto>.Success(ToDto(customer));
    }

    public Task<Result<CustomerAccountStatsDto>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Task.FromResult(Result<CustomerAccountStatsDto>.Unauthorized("Customer authentication is required."));
        }

        var customer = FindCustomerById(customerId.Value);
        if (customer is null)
        {
            return Task.FromResult(Result<CustomerAccountStatsDto>.NotFound("Customer was not found."));
        }

        var totalOrders = dbContext.Orders.Count(o => o.CustomerId == customerId.Value);
        var pendingOrders = dbContext.Orders.Count(o =>
            o.CustomerId == customerId.Value &&
            (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed));
        var shippingOrders = dbContext.Orders.Count(o =>
            o.CustomerId == customerId.Value &&
            (o.Status == OrderStatus.Processing || o.Status == OrderStatus.Shipping));

        var stats = new CustomerAccountStatsDto(
            totalOrders,
            pendingOrders,
            shippingOrders,
            customer.RewardPoints);

        return Task.FromResult(Result<CustomerAccountStatsDto>.Success(stats));
    }

    public Task<Result<IReadOnlyList<CustomerLoginHistoryDto>>> GetLoginHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Task.FromResult(Result<IReadOnlyList<CustomerLoginHistoryDto>>.Unauthorized("Customer authentication is required."));
        }

        var history = dbContext.CustomerLoginHistories
            .Where(h => h.CustomerId == customerId.Value)
            .OrderByDescending(h => h.LoginTime)
            .Take(20)
            .Select(h => new CustomerLoginHistoryDto(h.Id, h.LoginTime, h.IpAddress, h.UserAgent, h.Success))
            .ToArray();

        return Task.FromResult(Result<IReadOnlyList<CustomerLoginHistoryDto>>.Success(history));
    }

    public async Task<Result<CustomerProfileDto>> ToggleTwoFactorAsync(
        Toggle2FARequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        if (request.Enable)
        {
            // Generate a simulated TOTP secret
            var secret = GenerateTotpSecret();
            customer.EnableTwoFactor(secret);
        }
        else
        {
            customer.DisableTwoFactor();
        }

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
            customer.AvatarUrl,
            customer.IsEmailVerified,
            customer.RewardPoints,
            customer.TwoFactorEnabled,
            customer.CreatedAt,
            customer.UpdatedAt);
    }

    private static string GenerateTotpSecret()
    {
        // Generate a base32-encoded random secret (simulated TOTP)
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var rng = Random.Shared;
        return new string(Enumerable.Range(0, 32).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}
