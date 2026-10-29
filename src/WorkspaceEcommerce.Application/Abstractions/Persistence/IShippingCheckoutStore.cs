using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IShippingCheckoutStore
{
    IQueryable<ShippingQuote> Quotes { get; }
    IQueryable<OrderShippingSelection> Selections { get; }
    IQueryable<Domain.Modules.Payments.OrderPaymentReceipt> PaymentReceipts { get; }
    Task SavePaymentReceiptAsync(CancellationToken cancellationToken);
    Task<ShippingQuote?> LockQuoteAsync(Guid id, CancellationToken cancellationToken);
    Task<Cart?> LockCartAsync(string sessionId, CancellationToken cancellationToken);
    Task<Domain.Modules.Ordering.Order?> ReloadOrderForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<T> TransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
