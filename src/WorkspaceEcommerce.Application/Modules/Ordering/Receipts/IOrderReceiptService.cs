using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Ordering.Receipts;

public interface IOrderReceiptService
{
    Task<Result<OrderReceiptDocument>> GenerateForGuestAsync(
        OrderLookupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OrderReceiptDocument>> GenerateForCustomerAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Result<OrderReceiptDocument>> GenerateForAdminAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
