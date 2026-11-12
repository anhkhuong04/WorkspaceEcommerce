using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Application.Abstractions.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Queries;

internal sealed class EfAdminDashboardQuery(AppDbContext dbContext, IShippingCheckoutStore shipping) : IAdminDashboardQuery
{
    public async Task<decimal> GetNetCollectedRevenueAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Orders.AsNoTracking().Where(o => o.PaidAt != null && (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Returned))
            .Select(o => (decimal?)((o.TotalAmount - o.RefundedAmount) * o.ExchangeRate)).SumAsync(cancellationToken) ?? 0m;

    public async Task<IReadOnlyCollection<AdminOperationalQueueDto>> GetOperationsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<AdminOperationalQueueDto>();
        foreach (var (key, label) in new[] { ("transfers", "Verify bank transfers"), ("cod-remittance", "Reconcile collected COD"), ("delivery-failed", "Failed deliveries"), ("returns", "Review / receive returns"), ("refunds", "Refunds awaiting evidence") })
            result.Add(new(key, label, await AdminOrderQueues.Filter(dbContext, dbContext.Orders.AsNoTracking(), key, shipping).CountAsync(cancellationToken), $"/orders?queue={key}"));
        result.Add(new("payment-exceptions", "Reconcile payment differences", await dbContext.OrderPaymentExceptions.CountAsync(e => e.SettledAt == null, cancellationToken), "/orders?queue=payment-exceptions"));
        result.Add(new("comments", "Comments awaiting moderation", await dbContext.BlogComments.CountAsync(c => c.ModerationStatus == BlogCommentModerationStatus.Pending, cancellationToken), "/blogs?moderation=pending"));
        var failedWork = await dbContext.CustomerEmailOutboxMessages.CountAsync(e => e.DeadLetteredAt != null, cancellationToken)
            + await dbContext.ShipmentCommandOutbox.CountAsync(c => c.DeadLetteredAtUtc != null, cancellationToken);
        result.Add(new("operations", "Background failure records", failedWork, "/operations"));
        return result;
    }
    public async Task<IReadOnlyCollection<AdminOrderStatusSummaryDto>> GetOrderStatusSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.Status)
            .Select(group => new AdminOrderStatusSummaryDto(group.Key, group.Count()))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<decimal> GetCompletedRevenueAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Completed)
            .Select(order => (decimal?)order.TotalAmount)
            .SumAsync(cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyCollection<LowStockProductVariantDto>> GetLowStockVariantsAsync(
        int threshold,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var data = await (
                from variant in dbContext.ProductVariants.AsNoTracking()
                join product in dbContext.Products.AsNoTracking()
                    on variant.ProductId equals product.Id
                where variant.StockQuantity <= threshold
                orderby variant.StockQuantity, variant.Sku, variant.Id
                select new {
                    variant.ProductId,
                    ProductName = product.Name,
                    variant.Id,
                    variant.Sku,
                    variant.Name,
                    variant.StockQuantity,
                    variant.IsActive
                })
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return data.Select(x => new LowStockProductVariantDto(
            x.ProductId,
            x.ProductName.Get("en"),
            x.Id,
            x.Sku,
            x.Name,
            x.StockQuantity,
            x.IsActive)).ToArray();
    }

    public async Task<IReadOnlyCollection<RecentAdminOrderDto>> GetRecentOrdersAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Select(order => new RecentAdminOrderDto(
                order.Id,
                order.OrderCode,
                order.CustomerName,
                order.TotalAmount,
                order.Status,
                order.CreatedAt))
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }
}
