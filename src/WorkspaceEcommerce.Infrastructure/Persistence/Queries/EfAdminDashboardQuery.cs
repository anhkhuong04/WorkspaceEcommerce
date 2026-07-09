using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Queries;

internal sealed class EfAdminDashboardQuery(AppDbContext dbContext) : IAdminDashboardQuery
{
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
