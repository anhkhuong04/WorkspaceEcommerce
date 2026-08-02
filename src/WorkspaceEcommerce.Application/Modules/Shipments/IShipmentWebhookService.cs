using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public interface IShipmentWebhookService
{
    Task<Result<ShipmentWebhookResult>> HandleAsync(
        ShipmentWebhookPayload payload,
        CancellationToken cancellationToken = default);
}
