using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Customers.Orders;

public sealed record CustomerOrderListRequest
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public int PageNumber { get; init; } = DefaultPageNumber;

    public int PageSize { get; init; } = DefaultPageSize;

    public OrderStatus? Status { get; init; }

    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;

    public int NormalizedPageNumber => PageNumber < 1 ? DefaultPageNumber : PageNumber;

    public int NormalizedPageSize => PageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };
}
