namespace WorkspaceEcommerce.Application.Modules.Reviews;

public sealed record ReviewDto(
    Guid Id,
    Guid ProductId,
    Guid CustomerId,
    string CustomerName,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed record ProductReviewSummaryDto(
    double AverageRating,
    int ReviewCount,
    IReadOnlyCollection<ReviewDto> Reviews);

public sealed record AdminReviewListItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid CustomerId,
    string CustomerName,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);
