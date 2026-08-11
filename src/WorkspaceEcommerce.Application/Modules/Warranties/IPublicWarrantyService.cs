using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

public interface IPublicWarrantyService
{
    Task<Result<PublicWarrantyLookupResponse>> LookupAsync(WarrantyLookupRequest request, CancellationToken cancellationToken = default);
}
