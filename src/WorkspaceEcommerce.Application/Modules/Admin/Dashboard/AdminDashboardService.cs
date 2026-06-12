using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

internal sealed class AdminDashboardService(IAppDbContext dbContext) : IAdminDashboardService
{
    private const int LowStockThreshold = 5;
    private const int LowStockLimit = 10;
    private const int RecentOrderLimit = 5;

    public Task<Result<AdminDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var orders = dbContext.Orders.ToArray();
        var productsById = dbContext.Products.ToDictionary(product => product.Id);
        var lowStockVariantEntities = dbContext.ProductVariants
            .Where(variant => variant.StockQuantity <= LowStockThreshold)
            .OrderBy(variant => variant.StockQuantity)
            .ThenBy(variant => variant.Sku)
            .Take(LowStockLimit)
            .ToArray();
        var lowStockVariants = lowStockVariantEntities
            .Select(variant =>
            {
                productsById.TryGetValue(variant.ProductId, out var product);

                return new LowStockProductVariantDto(
                    variant.ProductId,
                    product?.Name ?? string.Empty,
                    variant.Id,
                    variant.Sku,
                    variant.Name,
                    variant.StockQuantity,
                    variant.IsActive);
            })
            .ToArray();
        var orderStatusSummary = Enum.GetValues<OrderStatus>()
            .Select(status => new AdminOrderStatusSummaryDto(
                status,
                orders.Count(order => order.Status == status)))
            .ToArray();
        var recentOrders = orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Take(RecentOrderLimit)
            .Select(order => new RecentAdminOrderDto(
                order.Id,
                order.OrderCode,
                order.CustomerName,
                order.TotalAmount,
                order.Status,
                order.CreatedAt))
            .ToArray();

        var dashboard = new AdminDashboardDto(
            orders.Length,
            orders.Where(order => order.Status == OrderStatus.Completed).Sum(order => order.TotalAmount),
            orders.Count(order => order.Status == OrderStatus.Pending),
            LowStockThreshold,
            lowStockVariants,
            orderStatusSummary,
            recentOrders);

        return Task.FromResult(Result<AdminDashboardDto>.Success(dashboard));
    }
}
