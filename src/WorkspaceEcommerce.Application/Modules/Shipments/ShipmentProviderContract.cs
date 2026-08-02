using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public static class ShipmentProviderContract
{
    public const string ProviderName = "MiniLogistics";
    public const string ShipmentCreatedEvent = "shipment.created";
    public const string ShipmentStatusChangedEvent = "shipment.status_changed";
    public const string WebhookTestEvent = "webhook.test";

    public static OrderStatus? MapOrderStatus(string providerStatus)
    {
        return providerStatus switch
        {
            "PendingPickup" => OrderStatus.Confirmed,
            "Assigned" or "PickingUp" or "PickedUp" => OrderStatus.Processing,
            "InTransit" or "Delivering" => OrderStatus.Shipping,
            "Delivered" => OrderStatus.Completed,
            "DeliveryFailed" or "Returned" => OrderStatus.FailedDelivery,
            "Cancelled" => OrderStatus.Cancelled,
            "Draft" => null,
            _ => null
        };
    }

    public static bool IsKnownStatus(string providerStatus)
    {
        return providerStatus is
            "Draft" or
            "PendingPickup" or
            "Assigned" or
            "PickingUp" or
            "PickedUp" or
            "InTransit" or
            "Delivering" or
            "Delivered" or
            "DeliveryFailed" or
            "Returned" or
            "Cancelled";
    }
}
