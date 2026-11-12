using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class OrderExpiryOptions
{
    public int VNPayMinutes { get; set; } = 30;
    public int ManualTransferMinutes { get; set; } = 1440;
}

public interface IOrderExpiryService
{
    Task<int> ExpireDueAsync(int batchSize, CancellationToken cancellationToken);
}

internal sealed class OrderExpiryService(IAppDbContext db, IFulfillmentCoordinator fulfillment, TimeProvider clock) : IOrderExpiryService
{
    public async Task<int> ExpireDueAsync(int batchSize, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ids = await db.Orders.AsNoTrackingIfEf().Where(order => order.PaymentDueAt <= now &&
                (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Confirmed) &&
                (order.PaymentStatus == PaymentStatus.Pending || order.PaymentStatus == PaymentStatus.Unpaid || order.PaymentStatus == PaymentStatus.Failed))
            .OrderBy(order => order.PaymentDueAt).ThenBy(order => order.Id).Take(Math.Clamp(batchSize, 1, 100)).Select(order => order.Id).ToArrayAsyncSafe(ct);
        var expired = 0;
        foreach (var id in ids)
            await db.ExecuteInTransactionAsync(async token =>
            {
                var payments = await db.FindPendingPaymentTransactionsForOrderForUpdateAsync(id, token);
                var order = await db.ReloadOrderForUpdateAsync(id, token);
                if (order is null || order.PaymentMethod == PaymentMethod.Cod || !order.PaymentDueAt.HasValue || order.PaymentDueAt > now ||
                    order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed) ||
                    order.PaymentStatus is not (PaymentStatus.Pending or PaymentStatus.Unpaid or PaymentStatus.Failed)) return;
                var previous = order.Status;
                const string reason = "Payment deadline expired.";
                db.Add(order.ChangeStatus(Guid.NewGuid(), OrderStatus.Cancelled, null, reason, reason, "Payment expiry worker"));
                await OrderCancellationEffects.ApplyAsync(db, order, previous, payments, reason, now, token);
                if (await fulfillment.MethodAsync(id, token) is not null) await fulfillment.CancelAsync(order, reason, token, "Payment expiry worker");
                await db.SaveChangesAsync(token);
                expired++;
            }, ct);
        return expired;
    }
}
