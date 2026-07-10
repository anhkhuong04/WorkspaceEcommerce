using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Loyalty;

internal sealed class LoyaltyService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    LoyaltyOptions options,
    IValidator<LoyaltyTransactionListRequest> transactionListValidator,
    IValidator<RedeemLoyaltyPointsRequest> redeemValidator) : ILoyaltyService
{
    public Task<Result<LoyaltyAccountDto>> GetMyLoyaltyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (currentCustomer.CustomerId is not { } customerId)
        {
            return Task.FromResult(Result<LoyaltyAccountDto>.Unauthorized("Customer authentication is required."));
        }

        var account = dbContext.CustomerLoyaltyAccounts.FirstOrDefault(existing => existing.CustomerId == customerId);
        var tiers = GetTierDefinitions();
        var dto = account is null
            ? ToEmptyAccountDto(customerId, tiers)
            : ToAccountDto(account, tiers);

        return Task.FromResult(Result<LoyaltyAccountDto>.Success(dto));
    }

    public async Task<Result<PagedResult<LoyaltyTransactionDto>>> GetMyTransactionsAsync(
        LoyaltyTransactionListRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await transactionListValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PagedResult<LoyaltyTransactionDto>>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        if (currentCustomer.CustomerId is not { } customerId)
        {
            return Result<PagedResult<LoyaltyTransactionDto>>.Unauthorized("Customer authentication is required.");
        }

        var account = dbContext.CustomerLoyaltyAccounts.FirstOrDefault(existing => existing.CustomerId == customerId);
        if (account is null)
        {
            return Result<PagedResult<LoyaltyTransactionDto>>.Success(
                PagedResult<LoyaltyTransactionDto>.Empty(request.NormalizedPageNumber, request.NormalizedPageSize));
        }

        var transactions = dbContext.LoyaltyTransactions
            .Where(transaction => transaction.CustomerLoyaltyAccountId == account.Id)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .ToArray();

        var page = new PagedResult<LoyaltyTransactionDto>(
            transactions
                .Skip(request.Skip)
                .Take(request.NormalizedPageSize)
                .Select(ToTransactionDto)
                .ToArray(),
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            transactions.Length);

        return Result<PagedResult<LoyaltyTransactionDto>>.Success(page);
    }

    public Task<Result<IReadOnlyCollection<LoyaltyTierDto>>> GetTiersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<LoyaltyTierDto> tiers = GetTierDefinitions()
            .Select(ToTierDto)
            .ToArray();

        return Task.FromResult(Result<IReadOnlyCollection<LoyaltyTierDto>>.Success(tiers));
    }

    public async Task<Result<RedeemLoyaltyPointsResponse>> RedeemPointsAsync(
        RedeemLoyaltyPointsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await redeemValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<RedeemLoyaltyPointsResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var optionsValidation = ValidateOptions();
        if (optionsValidation is not null)
        {
            return Result<RedeemLoyaltyPointsResponse>.Failure(optionsValidation);
        }

        if (currentCustomer.CustomerId is not { } customerId)
        {
            return Result<RedeemLoyaltyPointsResponse>.Unauthorized("Customer authentication is required.");
        }

        Result<RedeemLoyaltyPointsResponse>? result = null;

        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var account = dbContext.CustomerLoyaltyAccounts.FirstOrDefault(existing => existing.CustomerId == customerId);
                if (account is null)
                {
                    result = Result<RedeemLoyaltyPointsResponse>.Validation(["Loyalty account does not have enough points."]);
                    return;
                }

                var voucherId = Guid.NewGuid();
                var voucherCode = Coupon.FormatLoyaltyVoucherCode(voucherId);
                var discountAmount = request.Points * options.VoucherAmountPerPoint;
                var startsAt = DateTimeOffset.UtcNow;
                var expiresAt = startsAt.AddDays(options.VoucherValidityDays);
                var voucher = Coupon.CreateLoyaltyVoucher(
                    voucherId,
                    customerId,
                    voucherCode,
                    "Loyalty points voucher",
                    discountAmount,
                    startsAt,
                    expiresAt);

                var transaction = account.RedeemPoints(
                    request.Points,
                    voucher.Id,
                    $"Redeemed {request.Points} loyalty points for voucher {voucher.Code}.");

                dbContext.Add(voucher);
                dbContext.Add(transaction);
                dbContext.Update(account);
                await dbContext.SaveChangesAsync(transactionCancellationToken);

                result = Result<RedeemLoyaltyPointsResponse>.Success(new RedeemLoyaltyPointsResponse(
                    voucher.Id,
                    voucher.Code,
                    voucher.DiscountValue,
                    account.CurrentPoints,
                    voucher.EndsAt!.Value));
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<RedeemLoyaltyPointsResponse>.Validation([exception.Message]);
        }
        catch (PersistenceConcurrencyException)
        {
            return Result<RedeemLoyaltyPointsResponse>.Conflict("Loyalty points changed. Please try again.");
        }

        return result ?? Result<RedeemLoyaltyPointsResponse>.Failure("Could not redeem loyalty points.");
    }

    public async Task<Result> EarnForCompletedOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var optionsValidation = ValidateOptions();
        if (optionsValidation is not null)
        {
            return Result.Failure(optionsValidation);
        }

        if (orderId == Guid.Empty)
        {
            return Result.Validation(["Order id is required."]);
        }

        var order = dbContext.Orders.FirstOrDefault(existing => existing.Id == orderId);
        if (order is null)
        {
            return Result.NotFound("Order was not found.");
        }

        if (order.Status != OrderStatus.Completed)
        {
            return Result.Conflict("Order must be completed before earning loyalty points.");
        }

        if (order.CustomerId is not { } customerId)
        {
            return Result.Success();
        }

        if (dbContext.LoyaltyTransactions.Any(transaction =>
                transaction.Type == LoyaltyTransactionType.Earn &&
                transaction.OrderId == order.Id))
        {
            return Result.Success();
        }

        var points = CalculateEarnPoints(order);
        if (points <= 0)
        {
            return Result.Success();
        }

        try
        {
            var account = dbContext.CustomerLoyaltyAccounts.FirstOrDefault(existing => existing.CustomerId == customerId);
            var isNewAccount = account is null;
            account ??= new CustomerLoyaltyAccount(Guid.NewGuid(), customerId);

            var transaction = account.EarnPoints(
                points,
                order.Id,
                $"Earned {points} loyalty points from order {order.OrderCode}.");

            account.TryEvaluateTierUpgrade(GetTierDefinitions());

            if (isNewAccount)
            {
                dbContext.Add(account);
            }
            else
            {
                dbContext.Update(account);
            }

            dbContext.Add(transaction);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DomainException exception)
        {
            return exception.Message == "Loyalty points have already been earned for this order."
                ? Result.Success()
                : Result.Conflict(exception.Message);
        }
        catch (PersistenceConcurrencyException)
        {
            return Result.Conflict("Loyalty points changed. Please try again.");
        }
    }

    private int CalculateEarnPoints(Order order)
    {
        var paidMerchandiseAmount = Math.Max(0m, order.Subtotal - order.DiscountAmount);
        var normalizedAmount = paidMerchandiseAmount * order.ExchangeRate;

        return (int)Math.Floor(normalizedAmount / options.MoneyPerPoint);
    }

    private string[]? ValidateOptions()
    {
        var errors = options.Validate();

        return errors.Length == 0 ? null : errors;
    }

    private LoyaltyTier[] GetTierDefinitions()
    {
        return dbContext.LoyaltyTiers
            .OrderBy(tier => tier.MinTotalPointsEarned)
            .ThenBy(tier => tier.Type)
            .ToArray();
    }

    private static LoyaltyAccountDto ToEmptyAccountDto(Guid customerId, IReadOnlyCollection<LoyaltyTier> tiers)
    {
        var currentTier = tiers.FirstOrDefault(tier => tier.Type == LoyaltyTierType.Bronze);
        var nextTier = GetNextTier(LoyaltyTierType.Bronze, totalPointsEarned: 0, tiers);

        return new LoyaltyAccountDto(
            AccountId: null,
            customerId,
            CurrentPoints: 0,
            TotalPointsEarned: 0,
            LoyaltyTierType.Bronze,
            currentTier?.DiscountPercent ?? 0m,
            currentTier?.FreeShippingEnabled ?? false,
            nextTier?.Type,
            nextTier is null ? null : nextTier.MinTotalPointsEarned);
    }

    private static LoyaltyAccountDto ToAccountDto(CustomerLoyaltyAccount account, IReadOnlyCollection<LoyaltyTier> tiers)
    {
        var currentTier = tiers.FirstOrDefault(tier => tier.Type == account.CurrentTier);
        var nextTier = GetNextTier(account.CurrentTier, account.TotalPointsEarned, tiers);

        return new LoyaltyAccountDto(
            account.Id,
            account.CustomerId,
            account.CurrentPoints,
            account.TotalPointsEarned,
            account.CurrentTier,
            currentTier?.DiscountPercent ?? 0m,
            currentTier?.FreeShippingEnabled ?? false,
            nextTier?.Type,
            nextTier is null
                ? null
                : Math.Max(0, nextTier.MinTotalPointsEarned - account.TotalPointsEarned));
    }

    private static LoyaltyTier? GetNextTier(
        LoyaltyTierType currentTier,
        int totalPointsEarned,
        IReadOnlyCollection<LoyaltyTier> tiers)
    {
        return tiers
            .Where(tier => tier.Type > currentTier && tier.MinTotalPointsEarned > totalPointsEarned)
            .OrderBy(tier => tier.MinTotalPointsEarned)
            .ThenBy(tier => tier.Type)
            .FirstOrDefault();
    }

    private static LoyaltyTransactionDto ToTransactionDto(LoyaltyTransaction transaction)
    {
        return new LoyaltyTransactionDto(
            transaction.Id,
            transaction.Type,
            transaction.Points,
            transaction.BalanceAfter,
            transaction.OrderId,
            transaction.VoucherId,
            transaction.Description,
            transaction.CreatedAt);
    }

    private static LoyaltyTierDto ToTierDto(LoyaltyTier tier)
    {
        return new LoyaltyTierDto(
            tier.Id,
            tier.Type,
            tier.MinTotalPointsEarned,
            tier.DiscountPercent,
            tier.FreeShippingEnabled);
    }
}
