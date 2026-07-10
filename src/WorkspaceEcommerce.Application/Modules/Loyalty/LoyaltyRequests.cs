namespace WorkspaceEcommerce.Application.Modules.Loyalty;

public sealed record LoyaltyTransactionListRequest
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

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

public sealed class RedeemLoyaltyPointsRequest
{
    public int Points { get; init; }
}
