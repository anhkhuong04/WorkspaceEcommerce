using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Addresses;

internal sealed class CustomerAddressService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer) : ICustomerAddressService
{
    public Task<Result<IReadOnlyList<CustomerAddressDto>>> GetAddressesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Task.FromResult(Result<IReadOnlyList<CustomerAddressDto>>.Unauthorized("Customer authentication is required."));
        }

        var addresses = dbContext.CustomerAddresses
            .Where(a => a.CustomerId == customerId.Value)
            .OrderByDescending(a => a.IsDefault)
            .Select(ToDto)
            .ToArray();

        return Task.FromResult(Result<IReadOnlyList<CustomerAddressDto>>.Success(addresses));
    }

    public async Task<Result<CustomerAddressDto>> CreateAddressAsync(
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerAddressDto>.Unauthorized("Customer authentication is required.");
        }

        var existingCount = dbContext.CustomerAddresses
            .Count(a => a.CustomerId == customerId.Value);

        // First address is automatically default
        var isDefault = existingCount == 0;

        if (!isDefault && request.Label.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            isDefault = true;
        }

        if (isDefault)
        {
            // Clear existing defaults
            await ClearDefaultsAsync(customerId.Value, cancellationToken);
        }

        var address = new CustomerAddress(
            Guid.NewGuid(),
            customerId.Value,
            request.Label,
            request.ContactName,
            request.ContactPhone,
            request.Street,
            request.Ward,
            request.Province,
            isDefault);

        dbContext.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerAddressDto>.Success(ToDto(address));
    }

    public async Task<Result<CustomerAddressDto>> UpdateAddressAsync(
        Guid id,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerAddressDto>.Unauthorized("Customer authentication is required.");
        }

        var address = dbContext.CustomerAddresses.FirstOrDefault(a =>
            a.Id == id && a.CustomerId == customerId.Value);
        if (address is null)
        {
            return Result<CustomerAddressDto>.NotFound("Address was not found.");
        }

        address.Update(
            request.Label,
            request.ContactName,
            request.ContactPhone,
            request.Street,
            request.Ward,
            request.Province);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerAddressDto>.Success(ToDto(address));
    }

    public async Task<Result> DeleteAddressAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result.Unauthorized("Customer authentication is required.");
        }

        var address = dbContext.CustomerAddresses.FirstOrDefault(a =>
            a.Id == id && a.CustomerId == customerId.Value);
        if (address is null)
        {
            return Result.NotFound("Address was not found.");
        }

        dbContext.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        // If deleted address was default, promote the next one
        if (address.IsDefault)
        {
            var next = dbContext.CustomerAddresses
                .FirstOrDefault(a => a.CustomerId == customerId.Value);
            if (next is not null)
            {
                next.SetDefault(true);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success();
    }

    public async Task<Result<CustomerAddressDto>> SetDefaultAddressAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerAddressDto>.Unauthorized("Customer authentication is required.");
        }

        var address = dbContext.CustomerAddresses.FirstOrDefault(a =>
            a.Id == id && a.CustomerId == customerId.Value);
        if (address is null)
        {
            return Result<CustomerAddressDto>.NotFound("Address was not found.");
        }

        await ClearDefaultsAsync(customerId.Value, cancellationToken);
        address.SetDefault(true);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CustomerAddressDto>.Success(ToDto(address));
    }

    private async Task ClearDefaultsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var currentDefaults = dbContext.CustomerAddresses
            .Where(a => a.CustomerId == customerId && a.IsDefault)
            .ToArray();

        foreach (var addr in currentDefaults)
        {
            addr.SetDefault(false);
        }

        if (currentDefaults.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static CustomerAddressDto ToDto(CustomerAddress address)
    {
        return new CustomerAddressDto(
            address.Id,
            address.Label,
            address.ContactName,
            address.ContactPhone,
            address.Street,
            address.Ward,
            address.Province,
            address.IsDefault);
    }
}
