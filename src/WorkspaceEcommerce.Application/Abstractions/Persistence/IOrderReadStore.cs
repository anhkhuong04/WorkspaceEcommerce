using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IOrderReadStore
{
    IQueryable<Order> Orders { get; }

    IQueryable<OrderItem> OrderItems { get; }

    IQueryable<OrderStatusHistory> OrderStatusHistories { get; }

    IQueryable<OrderShipment> OrderShipments { get; }

    IQueryable<ShipmentTimelineEntry> ShipmentTimelineEntries { get; }

    IQueryable<ShipmentEventInbox> ShipmentEventInbox { get; }

    IQueryable<ShipmentCommandOutbox> ShipmentCommandOutbox { get; }
}
