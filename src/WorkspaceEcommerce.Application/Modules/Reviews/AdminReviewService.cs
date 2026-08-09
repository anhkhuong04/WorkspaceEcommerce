using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;

namespace WorkspaceEcommerce.Application.Modules.Reviews;

internal sealed class AdminReviewService(IAppDbContext db) : IAdminReviewService
{
    public async Task<Result<PagedResult<AdminReviewListItemDto>>> GetReviewsAsync(
        AdminReviewListRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query =
            from review in db.Reviews.AsNoTrackingIfEf()
            join product in db.Products.AsNoTrackingIfEf()
                on review.ProductId equals product.Id into products
            from product in products.DefaultIfEmpty()
            join customer in db.Customers.AsNoTrackingIfEf()
                on review.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            select new { review, product, customer };
        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.review.CreatedAt)
            .ThenByDescending(row => row.review.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.review.Id,
                row.review.ProductId,
                row.review.CustomerId,
                ProductName = row.product == null ? null : row.product.Name,
                CustomerName = row.customer == null ? null : row.customer.FullName,
                row.review.Rating,
                row.review.Comment,
                row.review.CreatedAt
            })
            .ToArrayAsyncSafe(cancellationToken);
        var items = rows.Select(row => new AdminReviewListItemDto(
            row.Id,
            row.ProductId,
            row.ProductName?.Get("en") ?? "Unknown Product",
            row.CustomerId,
            row.CustomerName ?? "Unknown Customer",
            row.Rating,
            row.Comment,
            row.CreatedAt)).ToArray();

        return Result<PagedResult<AdminReviewListItemDto>>.Success(
            new PagedResult<AdminReviewListItemDto>(items, page, pageSize, totalCount));
    }

    public async Task<Result> DeleteReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var review = await db.Reviews
            .Where(r => r.Id == reviewId)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        if (review is null)
        {
            return Result.NotFound("Review was not found.");
        }

        var product = await db.Products
            .Where(p => p.Id == review.ProductId)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        db.Remove(review);

        if (product is not null)
        {
            // Recalculate stats after deletion
            var ratingSummary = await db.Reviews
                .Where(r => r.ProductId == review.ProductId && r.Id != reviewId)
                .GroupBy(_ => 1)
                .Select(group => new { Total = group.Sum(value => value.Rating), Count = group.Count() })
                .FirstOrDefaultAsyncSafe(cancellationToken);
            var count = ratingSummary?.Count ?? 0;
            product.UpdateRatingStats(count == 0 ? 0 : ratingSummary!.Total / (double)count, count);
            db.Update(product);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
