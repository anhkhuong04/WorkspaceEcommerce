using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public interface IStorefrontOrderLookupService
{
    Task<Result<OrderLookupResponse>> LookupAsync(
        OrderLookupRequest request,
        CancellationToken cancellationToken = default);
}
