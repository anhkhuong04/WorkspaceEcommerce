using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public interface IOrderShipmentService
{
    Task<Result<ShipmentTrackingDto>> GetGuestTrackingAsync(
        string orderCode,
        string phone,
        CancellationToken cancellationToken = default);

    Task<Result<ShipmentTrackingDto>> GetCustomerTrackingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Result<ShipmentTrackingDto>> GetAdminTrackingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Result<ShipmentTrackingDto>> RefreshAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Result<ShipmentTrackingDto>> RetryCreateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Result<ShipmentTrackingDto>> CancelAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default);

    Task QueueCancelAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<int> ProcessDueCommandsAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
