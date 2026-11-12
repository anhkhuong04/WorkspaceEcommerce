using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;

namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public sealed record AdminSearchItemDto(string Kind, Guid Id, string Title, string? Detail, string Url);
public sealed record AdminSearchDto(IReadOnlyCollection<AdminSearchItemDto> Items);
public interface IAdminSearchService
{
    Task<Result<AdminSearchDto>> SearchAsync(string? search, CancellationToken cancellationToken = default);
}

internal sealed class AdminSearchService(IAppDbContext db) : IAdminSearchService
{
    public async Task<Result<AdminSearchDto>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var term = search?.Trim().ToLowerInvariant() ?? "";
        if (term.Length is < 2 or > 150) return Result<AdminSearchDto>.Validation(["Enter between 2 and 150 characters."]);
        const int limit = 5;
        var found = new List<AdminSearchItemDto>();
        var productNames = db.SearchProducts(term).Select(p => p.Id);
        var products = await db.Products.AsNoTrackingIfEf().Where(p => productNames.Contains(p.Id) ||
            db.ProductVariants.Any(v => v.ProductId == p.Id && v.Sku.ToLower().Contains(term)))
            .OrderBy(p => p.Slug).ThenBy(p => p.Id).Take(limit).Select(p => new { p.Id, p.Name, p.Slug }).ToArrayAsyncSafe(ct);
        found.AddRange(products.Select(p => new AdminSearchItemDto("Products", p.Id, p.Name.Get("vi"), p.Slug, $"/products?productId={p.Id}")));
        var orders = await db.Orders.AsNoTrackingIfEf().Where(o => o.OrderCode.ToLower().Contains(term) ||
            o.CustomerName.ToLower().Contains(term) || o.CustomerPhone.Contains(term) || (o.TrackingCode != null && o.TrackingCode.ToLower().Contains(term)))
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Take(limit)
            .Select(o => new AdminSearchItemDto("Orders", o.Id, o.OrderCode, o.CustomerName, "/orders?orderId=" + o.Id)).ToArrayAsyncSafe(ct);
        found.AddRange(orders);
        var categories = await db.SearchCategories(term).AsNoTrackingIfEf().OrderBy(c => c.Slug).ThenBy(c => c.Id).Take(limit)
            .Select(c => new { c.Id, c.Name, c.Slug }).ToArrayAsyncSafe(ct);
        found.AddRange(categories.Select(c => new AdminSearchItemDto("Categories", c.Id, c.Name.Get("vi"), c.Slug, $"/categories?categoryId={c.Id}")));
        var coupons = await db.Coupons.AsNoTrackingIfEf().Where(c => c.Code.ToLower().Contains(term) || c.Name.ToLower().Contains(term))
            .OrderBy(c => c.Code).ThenBy(c => c.Id).Take(limit)
            .Select(c => new AdminSearchItemDto("Coupons", c.Id, c.Code, c.Name, "/coupons?search=" + c.Code)).ToArrayAsyncSafe(ct);
        found.AddRange(coupons);
        var posts = await db.BlogPosts.AsNoTrackingIfEf().Where(p => p.Title.ToLower().Contains(term) || p.Slug.Contains(term))
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Take(limit)
            .Select(p => new AdminSearchItemDto("Articles", p.Id, p.Title, p.Slug, "/blogs?postId=" + p.Id)).ToArrayAsyncSafe(ct);
        found.AddRange(posts);
        return Result<AdminSearchDto>.Success(new(found));
    }
}
