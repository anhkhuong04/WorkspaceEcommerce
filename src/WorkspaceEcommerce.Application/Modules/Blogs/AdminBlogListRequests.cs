using FluentValidation;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed record AdminBlogListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public bool? IsPublished { get; init; }
    public Guid? PostId { get; init; }
}
public sealed record AdminCommentListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? PostId { get; init; }
    public BlogCommentModerationStatus? Status { get; init; }
}
public sealed class AdminBlogListRequestValidator : AbstractValidator<AdminBlogListRequest>
{
    public AdminBlogListRequestValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(150);
    }
}
public sealed class AdminCommentListRequestValidator : AbstractValidator<AdminCommentListRequest>
{
    public AdminCommentListRequestValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}
