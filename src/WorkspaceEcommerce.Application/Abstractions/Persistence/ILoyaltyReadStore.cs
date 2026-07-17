using WorkspaceEcommerce.Domain.Modules.Loyalty;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface ILoyaltyReadStore
{
    IQueryable<CustomerLoyaltyAccount> CustomerLoyaltyAccounts { get; }

    IQueryable<LoyaltyTransaction> LoyaltyTransactions { get; }

    IQueryable<LoyaltyTier> LoyaltyTiers { get; }
}
