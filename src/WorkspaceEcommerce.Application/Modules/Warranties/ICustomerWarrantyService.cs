using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

public interface ICustomerWarrantyService
{
    Task<Result<CustomerWarrantyDto>> ActivateAsync(ActivateWarrantyRequest request, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<CustomerWarrantyListItemDto>>> GetWarrantiesAsync(CustomerWarrantyListRequest request, CancellationToken cancellationToken = default);
    Task<Result<CustomerWarrantyDto>> GetWarrantyAsync(Guid id, CancellationToken cancellationToken = default);
}
