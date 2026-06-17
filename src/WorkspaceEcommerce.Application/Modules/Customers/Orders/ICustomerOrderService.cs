using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

public interface ICustomerOrderService
{
    Task<Result<PagedResult<CustomerOrderListItemDto>>> GetOrdersAsync(
        CustomerOrderListRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerOrderDto>> GetOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
