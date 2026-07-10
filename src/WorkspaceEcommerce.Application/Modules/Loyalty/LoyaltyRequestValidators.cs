using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Loyalty;

public sealed class LoyaltyTransactionListRequestValidator : AbstractValidator<LoyaltyTransactionListRequest>
{
    public LoyaltyTransactionListRequestValidator()
    {
        RuleFor(request => request.PageNumber)
            .GreaterThan(0);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);
    }
}

public sealed class RedeemLoyaltyPointsRequestValidator : AbstractValidator<RedeemLoyaltyPointsRequest>
{
    public RedeemLoyaltyPointsRequestValidator()
    {
        RuleFor(request => request.Points)
            .GreaterThan(0);
    }
}
