using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Reviews;

public interface IReviewService
{
    /// <summary>Creates a review for a product. Only customers who have purchased the product can review.</summary>
    Task<Result<ReviewDto>> CreateReviewAsync(
        CreateReviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the reviews and rating summary for a product by slug.</summary>
    Task<Result<ProductReviewSummaryDto>> GetProductReviewsAsync(
        string slug,
        CancellationToken cancellationToken = default);
}

public interface IAdminReviewService
{
    /// <summary>Gets a paged list of all reviews for the admin.</summary>
    Task<Result<PagedResult<AdminReviewListItemDto>>> GetReviewsAsync(
        AdminReviewListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a review and recalculates the product rating stats.</summary>
    Task<Result> DeleteReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default);
}
