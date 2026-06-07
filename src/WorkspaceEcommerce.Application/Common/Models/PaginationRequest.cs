namespace WorkspaceEcommerce.Application.Common.Models;

public sealed record PaginationRequest
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int PageNumber { get; init; } = DefaultPageNumber;

    public int PageSize { get; init; } = DefaultPageSize;

    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;

    public int NormalizedPageNumber => PageNumber < 1 ? DefaultPageNumber : PageNumber;

    public int NormalizedPageSize => PageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };
}
