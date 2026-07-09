using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Reviews;

internal sealed class AdminReviewService(IAppDbContext db) : IAdminReviewService
{
    public Task<Result<PagedResult<AdminReviewListItemDto>>> GetReviewsAsync(
        AdminReviewListRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var productsById = db.Products.ToDictionary(p => p.Id, p => p.Name);
        var customersById = db.Customers.ToDictionary(c => c.Id, c => c.FullName);

        var allReviews = db.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .ToArray();

        var totalCount = allReviews.Length;

        var items = allReviews
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminReviewListItemDto(
                r.Id,
                r.ProductId,
                productsById.TryGetValue(r.ProductId, out var productName) ? productName : "Unknown Product",
                r.CustomerId,
                customersById.TryGetValue(r.CustomerId, out var customerName) ? customerName : "Unknown Customer",
                r.Rating,
                r.Comment,
                r.CreatedAt))
            .ToArray();

        return Task.FromResult(Result<PagedResult<AdminReviewListItemDto>>.Success(
            new PagedResult<AdminReviewListItemDto>(items, page, pageSize, totalCount)));
    }

    public async Task<Result> DeleteReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var review = db.Reviews.FirstOrDefault(r => r.Id == reviewId);

        if (review is null)
        {
            return Result.NotFound("Review was not found.");
        }

        var product = db.Products.FirstOrDefault(p => p.Id == review.ProductId);

        db.Remove(review);

        if (product is not null)
        {
            // Recalculate stats after deletion
            var remainingRatings = db.Reviews
                .Where(r => r.ProductId == review.ProductId && r.Id != reviewId)
                .Select(r => r.Rating)
                .ToList();

            var newAverage = remainingRatings.Count > 0 ? remainingRatings.Average() : 0.0;
            product.UpdateRatingStats(newAverage, remainingRatings.Count);
            db.Update(product);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
