using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Loyalty;

public interface ILoyaltyService
{
    Task<Result<LoyaltyAccountDto>> GetMyLoyaltyAsync(CancellationToken cancellationToken = default);

    Task<Result<PagedResult<LoyaltyTransactionDto>>> GetMyTransactionsAsync(
        LoyaltyTransactionListRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<LoyaltyTierDto>>> GetTiersAsync(CancellationToken cancellationToken = default);

    Task<Result<RedeemLoyaltyPointsResponse>> RedeemPointsAsync(
        RedeemLoyaltyPointsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> EarnForCompletedOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
