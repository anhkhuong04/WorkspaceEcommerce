using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Reviews;

namespace WorkspaceEcommerce.Application.Modules.Reviews;

internal sealed class ReviewService(
    IAppDbContext db,
    ICurrentCustomerContext currentCustomer,
    IValidator<CreateReviewRequest> validator) : IReviewService
{
    private const int MaxProductReviews = 100;

    public async Task<Result<ReviewDto>> CreateReviewAsync(
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentCustomer.CustomerId is null)
        {
            return Result<ReviewDto>.Unauthorized("You must be logged in to submit a review.");
        }

        var customerId = currentCustomer.CustomerId.Value;

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<ReviewDto>.Validation(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Ensure product exists
        var product = await db.Products
            .Where(p => p.Slug == request.Slug && p.IsActive)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        if (product is null)
        {
            return Result<ReviewDto>.NotFound("Product was not found.");
        }

        // Verify customer has purchased this product via a completed/shipping order
        var hasPurchased = await db.OrderItems
            .Join(db.Orders, item => item.OrderId, order => order.Id, (item, order) => new { item, order })
            .Join(db.ProductVariants, x => x.item.ProductVariantId, v => v.Id, (x, v) => new { x.item, x.order, variant = v })
            .Where(x => x.order.CustomerId == customerId
                        && x.variant.ProductId == product.Id
                        && (x.order.Status == OrderStatus.Completed
                            || x.order.Status == OrderStatus.Shipping))
            .AnyAsyncSafe(cancellationToken);

        if (!hasPurchased)
        {
            return Result<ReviewDto>.Validation(["You can only review products you have purchased."]);
        }

        // Check if already reviewed
        var alreadyReviewed = await db.Reviews
            .Where(r => r.ProductId == product.Id && r.CustomerId == customerId)
            .AnyAsyncSafe(cancellationToken);

        if (alreadyReviewed)
        {
            return Result<ReviewDto>.Conflict("You have already reviewed this product.");
        }

        // Create the review
        Review review;
        try
        {
            review = new Review(Guid.NewGuid(), product.Id, customerId, request.Rating, request.Comment);
        }
        catch (DomainException ex)
        {
            return Result<ReviewDto>.Validation([ex.Message]);
        }

        db.Add(review);

        // Recalculate product rating stats including the new rating
        var ratingSummary = await db.Reviews
            .Where(r => r.ProductId == product.Id)
            .GroupBy(_ => 1)
            .Select(group => new { Total = group.Sum(review => review.Rating), Count = group.Count() })
            .FirstOrDefaultAsyncSafe(cancellationToken);
        var newCount = (ratingSummary?.Count ?? 0) + 1;
        var newAverage = ((ratingSummary?.Total ?? 0) + request.Rating) / (double)newCount;
        product.UpdateRatingStats(newAverage, newCount);
        db.Update(product);

        await db.SaveChangesAsync(cancellationToken);

        // Get customer name for response
        var customer = await db.Customers
            .AsNoTrackingIfEf()
            .Where(c => c.Id == customerId)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return Result<ReviewDto>.Success(ToDto(review, customer?.FullName ?? "Customer"));
    }

    public async Task<Result<ProductReviewSummaryDto>> GetProductReviewsAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var product = await db.Products
            .AsNoTrackingIfEf()
            .Where(p => p.Slug == slug && p.IsActive)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        if (product is null)
        {
            return Result<ProductReviewSummaryDto>.NotFound("Product was not found.");
        }

        var reviews = await (
                from review in db.Reviews.AsNoTrackingIfEf()
                join customer in db.Customers.AsNoTrackingIfEf()
                    on review.CustomerId equals customer.Id into customers
                from customer in customers.DefaultIfEmpty()
                where review.ProductId == product.Id
                orderby review.CreatedAt descending, review.Id descending
                select new ReviewDto(
                    review.Id,
                    review.ProductId,
                    review.CustomerId,
                    customer == null ? "Customer" : customer.FullName,
                    review.Rating,
                    review.Comment,
                    review.CreatedAt))
            .Take(MaxProductReviews)
            .ToArrayAsyncSafe(cancellationToken);

        var summary = new ProductReviewSummaryDto(
            product.AverageRating,
            product.ReviewCount,
            reviews);

        return Result<ProductReviewSummaryDto>.Success(summary);
    }

    private static ReviewDto ToDto(Review review, string customerName)
    {
        return new ReviewDto(
            review.Id,
            review.ProductId,
            review.CustomerId,
            customerName,
            review.Rating,
            review.Comment,
            review.CreatedAt);
    }
}
