using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Reviews;

namespace WorkspaceEcommerce.Application.Modules.Reviews;

internal sealed class ReviewService(
    IAppDbContext db,
    ICurrentCustomerContext currentCustomer,
    IValidator<CreateReviewRequest> validator) : IReviewService
{
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
        var product = db.Products
            .FirstOrDefault(p => p.Slug == request.Slug && p.IsActive);

        if (product is null)
        {
            return Result<ReviewDto>.NotFound("Product was not found.");
        }

        // Verify customer has purchased this product via a completed/shipping order
        var hasPurchased = db.OrderItems
            .Join(db.Orders, item => item.OrderId, order => order.Id, (item, order) => new { item, order })
            .Join(db.ProductVariants, x => x.item.ProductVariantId, v => v.Id, (x, v) => new { x.item, x.order, variant = v })
            .Any(
                x => x.order.CustomerId == customerId
                     && x.variant.ProductId == product.Id
                     && (x.order.Status == OrderStatus.Completed
                         || x.order.Status == OrderStatus.Shipping));

        if (!hasPurchased)
        {
            return Result<ReviewDto>.Validation(["You can only review products you have purchased."]);
        }

        // Check if already reviewed
        var alreadyReviewed = db.Reviews
            .Any(r => r.ProductId == product.Id && r.CustomerId == customerId);

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
        var existingRatings = db.Reviews
            .Where(r => r.ProductId == product.Id)
            .Select(r => r.Rating)
            .ToList();

        existingRatings.Add(request.Rating);
        var newAverage = existingRatings.Average();
        var newCount = existingRatings.Count;
        product.UpdateRatingStats(newAverage, newCount);
        db.Update(product);

        await db.SaveChangesAsync(cancellationToken);

        // Get customer name for response
        var customer = db.Customers.FirstOrDefault(c => c.Id == customerId);

        return Result<ReviewDto>.Success(ToDto(review, customer?.FullName ?? "Customer"));
    }

    public Task<Result<ProductReviewSummaryDto>> GetProductReviewsAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var product = db.Products
            .FirstOrDefault(p => p.Slug == slug && p.IsActive);

        if (product is null)
        {
            return Task.FromResult(Result<ProductReviewSummaryDto>.NotFound("Product was not found."));
        }

        var customersById = db.Customers.ToDictionary(c => c.Id, c => c.FullName);

        var reviews = db.Reviews
            .Where(r => r.ProductId == product.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToArray()
            .Select(r => new ReviewDto(
                r.Id,
                r.ProductId,
                r.CustomerId,
                customersById.GetValueOrDefault(r.CustomerId, "Customer"),
                r.Rating,
                r.Comment,
                r.CreatedAt))
            .ToArray();

        var summary = new ProductReviewSummaryDto(
            product.AverageRating,
            product.ReviewCount,
            reviews);

        return Task.FromResult(Result<ProductReviewSummaryDto>.Success(summary));
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
