using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

internal static class ShipmentOrderStatusTransitioner
{
    private const string ChangedBy = "MiniLogistics Webhook";

    public static bool TryTransition(
        Order order,
        OrderStatus targetStatus,
        string providerStatus,
        IAppWriteStore writeStore)
    {
        if (order.Status == targetStatus)
        {
            return false;
        }

        var changed = false;
        var targetRank = Rank(targetStatus);

        while (order.Status != targetStatus)
        {
            var next = GetNextStatus(order.Status, targetStatus);
            if (next is null || Rank(next.Value) > targetRank)
            {
                return changed;
            }

            try
            {
                var history = order.ChangeStatus(
                    Guid.NewGuid(),
                    next.Value,
                    $"MiniLogistics status: {providerStatus}",
                    ChangedBy);
                writeStore.Add(history);
                changed = true;
            }
            catch (DomainException)
            {
                return changed;
            }
        }

        return changed;
    }

    private static OrderStatus? GetNextStatus(OrderStatus current, OrderStatus target)
    {
        if (target == OrderStatus.Cancelled)
        {
            return current is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.FailedDelivery
                ? OrderStatus.Cancelled
                : null;
        }

        return current switch
        {
            OrderStatus.Pending => OrderStatus.Confirmed,
            OrderStatus.Confirmed => OrderStatus.Processing,
            OrderStatus.Processing => OrderStatus.Shipping,
            OrderStatus.Shipping when target == OrderStatus.FailedDelivery => OrderStatus.FailedDelivery,
            OrderStatus.Shipping when target == OrderStatus.Completed => OrderStatus.Completed,
            _ => null
        };
    }

    private static int Rank(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => 0,
            OrderStatus.Confirmed => 1,
            OrderStatus.Processing => 2,
            OrderStatus.Shipping => 3,
            OrderStatus.Completed or OrderStatus.FailedDelivery or OrderStatus.Cancelled => 4,
            OrderStatus.Returned => 5,
            _ => int.MaxValue
        };
    }
}
