using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public static class AdminOrderQueues
{
    public static IQueryable<Order> Filter(IAppDbContext db, IQueryable<Order> orders, string? queue, IShippingCheckoutStore? shipping = null)
    {
        var receipts = shipping?.PaymentReceipts ?? Array.Empty<OrderPaymentReceipt>().AsQueryable();
        return queue switch {
        "transfers" => orders.Where(o => o.PaymentMethod == PaymentMethod.ManualBankTransfer && o.PaymentStatus == PaymentStatus.Pending && o.Status != OrderStatus.Cancelled &&
            (o.ImportSource != null || o.PaymentDueAt != null)),
        "cod-remittance" => orders.Where(o => o.PaymentMethod == PaymentMethod.Cod && receipts.Any(r => r.OrderId == o.Id && r.Kind == OrderPaymentReceiptKind.CodCollected) &&
            !receipts.Any(r => r.OrderId == o.Id && r.Kind == OrderPaymentReceiptKind.CodRemitted)),
        "delivery-failed" => orders.Where(o => o.Status == OrderStatus.FailedDelivery),
        "returns" => orders.Where(o => o.ReturnRequestedAt != null && o.ReturnReviewedAt == null || db.OrderReturns.Any(r => r.OrderId == o.Id && r.Status == OrderReturnStatus.AwaitingReceipt)),
        "refunds" => orders.Where(o => o.PaymentStatus == PaymentStatus.RefundPending),
        "payment-exceptions" => orders.Where(o => db.OrderPaymentExceptions.Any(e => e.OrderId == o.Id && e.SettledAt == null)),
        _ => orders
        };
    }
}
