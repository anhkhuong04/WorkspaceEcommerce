using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Customers.Addresses;

public interface ICustomerAddressService
{
    Task<Result<IReadOnlyList<CustomerAddressDto>>> GetAddressesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAddressDto>> CreateAddressAsync(
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAddressDto>> UpdateAddressAsync(
        Guid id,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAddressAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerAddressDto>> SetDefaultAddressAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
