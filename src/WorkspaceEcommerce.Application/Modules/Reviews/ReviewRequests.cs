namespace WorkspaceEcommerce.Application.Modules.Reviews;

public sealed record CreateReviewRequest(
    string Slug,
    int Rating,
    string? Comment);

public sealed record AdminReviewListRequest(
    int Page = 1,
    int PageSize = 20);
