using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Queries;

internal sealed class EfShippingCheckoutStore(AppDbContext context) : IShippingCheckoutStore
{
    public IQueryable<ShippingQuote> Quotes => context.Set<ShippingQuote>();
    public IQueryable<OrderShippingSelection> Selections => context.Set<OrderShippingSelection>();
    public Task<ShippingQuote?> LockQuoteAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<ShippingQuote>().FromSqlInterpolated($"SELECT * FROM shipping.quotes WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    public async Task<Cart?> LockCartAsync(string sessionId, CancellationToken cancellationToken)
    {
        var cart = await context.Set<Cart>().FromSqlInterpolated($"SELECT * FROM cart.carts WHERE session_id = {sessionId} ORDER BY id LIMIT 1 FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (cart is not null)
        {
            await context.Set<CartItem>().FromSqlInterpolated($"SELECT * FROM cart.cart_items WHERE cart_id = {cart.Id} ORDER BY id FOR UPDATE")
                .LoadAsync(cancellationToken);
            await context.Entry(cart).Collection(x => x.Items).LoadAsync(cancellationToken);
        }
        return cart;
    }
    public async Task<T> TransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
