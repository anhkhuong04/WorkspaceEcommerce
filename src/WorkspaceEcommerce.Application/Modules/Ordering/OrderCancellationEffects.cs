using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

// Caller holds payment -> order locks and applies the status transition once.
internal static class OrderCancellationEffects
{
    public static async Task ApplyAsync(IAppDbContext db, Order order, OrderStatus previousStatus,
        IReadOnlyCollection<PaymentTransaction> pendingPayments, string reason, DateTimeOffset now, CancellationToken ct)
    {
        if (previousStatus is OrderStatus.Pending or OrderStatus.Confirmed)
        {
            var items = await db.OrderItems.Where(item => item.OrderId == order.Id).ToArrayAsyncSafe(ct);
            var variants = (await db.FindProductVariantsForUpdateAsync(items.Select(item => item.ProductVariantId).Distinct().ToArray(), ct))
                .ToDictionary(variant => variant.Id);
            foreach (var item in items)
                if (variants.TryGetValue(item.ProductVariantId, out var variant)) variant.RestoreStock(item.Quantity);
            var redemptions = await db.CouponRedemptions.Where(redemption => redemption.OrderId == order.Id && redemption.ReleasedAt == null)
                .OrderBy(redemption => redemption.CouponId).ToArrayAsyncSafe(ct);
            foreach (var redemption in redemptions)
            {
                var coupon = await db.FindCouponForUpdateAsync(redemption.CouponId, ct);
                if (coupon is null) throw new InvalidOperationException("The redeemed coupon no longer exists.");
                coupon.ReleaseUsage();
                redemption.Release(now);
            }
        }
        // Failed delivery does not prove the shop has received and inspected stock.
        foreach (var payment in pendingPayments)
            payment.MarkCancelled(null, "ORDER_CANCELLED", reason, null, null, now);
        if (order.PaymentStatus == PaymentStatus.Paid) order.MarkPaymentRefundPending();
        else if (order.PaymentStatus is not (PaymentStatus.Cancelled or PaymentStatus.RefundPending or PaymentStatus.Refunded)) order.MarkPaymentCancelled();
    }
}
