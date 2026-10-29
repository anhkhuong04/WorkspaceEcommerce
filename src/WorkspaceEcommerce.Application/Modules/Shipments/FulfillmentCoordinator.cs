using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public interface IFulfillmentCoordinator
{
    Task<ShippingMethod?> MethodAsync(Guid orderId, CancellationToken cancellationToken);
    Task AcceptAsync(Order order, ShippingMethod? method, CancellationToken cancellationToken);
    Task CancelAsync(Order order, string reason, CancellationToken cancellationToken);
    Task GuardStatusAsync(Order order, OrderStatus status, CancellationToken cancellationToken);
    Task<OrderStatus?> ProviderTargetAsync(Order order, OrderStatus? target, CancellationToken cancellationToken);
}

internal sealed class FulfillmentCoordinator(IAppDbContext db, IShippingCheckoutStore shipping, TimeProvider clock) : IFulfillmentCoordinator
{
    public Task<ShippingMethod?> MethodAsync(Guid orderId, CancellationToken cancellationToken) =>
        shipping.Selections.Where(x => x.OrderId == orderId).Select(x => (ShippingMethod?)x.Method).SingleOrDefaultAsync(cancellationToken);

    // Called with the order lock, or inside the transaction inserting a new order.
    public async Task AcceptAsync(Order order, ShippingMethod? method, CancellationToken ct)
    {
        method ??= await MethodAsync(order.Id, ct);
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Returned or OrderStatus.Completed) return;
        if (method == ShippingMethod.WorkspaceEC)
        {
            if (await db.OrderShipments.AnyAsync(x => x.OrderId == order.Id, ct)) return;
            var now = clock.GetUtcNow();
            var shipment = OrderShipment.CreateWorkspace(Guid.NewGuid(), order.Id, $"WEC-{order.Id:N}".ToUpperInvariant(), now);
            db.Add(shipment);
            db.Add(new ShipmentTimelineEntry(Guid.NewGuid(), shipment.Id, shipment.ProviderStatus,
                "WorkspaceEC work accepted; dispatch requires order confirmation and eligible payment.", now, ShipmentTimelineSource.ShipmentCreated, null, now));
            return;
        }
        if (method.HasValue && method != ShippingMethod.MiniLogistics) throw new DomainException("Unsupported persisted shipping method.");
        if (method.HasValue && order.PaymentMethod != PaymentMethod.Cod && order.PaymentStatus != PaymentStatus.Paid) return;
        // No selection deliberately retains legacy behavior.
        if (!order.ShipmentId.HasValue)
            await db.TryEnqueueShipmentCommandAsync(order.Id, ShipmentCommandType.Create, null, clock.GetUtcNow(), ct);
    }

    public async Task CancelAsync(Order order, string reason, CancellationToken ct)
    {
        if (await MethodAsync(order.Id, ct) == ShippingMethod.WorkspaceEC)
        {
            var shipment = await db.FindOrderShipmentForUpdateAsync(order.Id, ct);
            if (shipment is null || !shipment.CancelWorkspace(clock.GetUtcNow())) return;
            var now = clock.GetUtcNow();
            db.Add(new ShipmentTimelineEntry(Guid.NewGuid(), shipment.Id, shipment.ProviderStatus, reason,
                now, ShipmentTimelineSource.Cancellation, null, now));
        }
        else
            await db.TryEnqueueShipmentCommandAsync(order.Id, ShipmentCommandType.Cancel, reason, clock.GetUtcNow(), ct);
    }

    public async Task GuardStatusAsync(Order order, OrderStatus status, CancellationToken ct)
    {
        var method = await MethodAsync(order.Id, ct);
        if (!method.HasValue) return;
        if (status == OrderStatus.Shipping && order.PaymentMethod != PaymentMethod.Cod && order.PaymentStatus != PaymentStatus.Paid)
            throw new DomainException("Prepaid delivery requires verified payment.");
        if (status == OrderStatus.Completed && order.PaymentMethod == PaymentMethod.Cod && order.PaymentStatus != PaymentStatus.Paid)
            throw new DomainException("COD completion requires confirmed full collection.");
        if (method == ShippingMethod.WorkspaceEC && status is OrderStatus.Shipping or OrderStatus.Completed or OrderStatus.FailedDelivery)
            throw new DomainException("Use the WorkspaceEC delivery lifecycle commands; direct order status changes cannot confirm delivery.");
    }

    public async Task<OrderStatus?> ProviderTargetAsync(Order order, OrderStatus? target, CancellationToken ct)
    {
        if (!target.HasValue || await MethodAsync(order.Id, ct) is null) return target;
        if (target is OrderStatus.Shipping or OrderStatus.Completed && order.PaymentMethod != PaymentMethod.Cod && order.PaymentStatus != PaymentStatus.Paid) return null;
        return target == OrderStatus.Completed && order.PaymentMethod == PaymentMethod.Cod && order.PaymentStatus != PaymentStatus.Paid ? OrderStatus.Shipping : target;
    }
}
