using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Application.Modules.Coupons;

internal sealed class AdminCouponService(
    IAppDbContext dbContext,
    IValidator<AdminCouponListRequest> listValidator,
    IValidator<CreateCouponRequest> createValidator,
    IValidator<UpdateCouponRequest> updateValidator,
    IValidator<UpdateCouponStatusRequest> statusValidator) : IAdminCouponService
{
    public async Task<Result<PagedResult<AdminCouponDto>>> GetCouponsAsync(
        AdminCouponListRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await listValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PagedResult<AdminCouponDto>>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSearch = NormalizeOptional(request.Search);
        var productTargetsByCouponId = GetProductTargetsByCouponId();
        var redemptionCountsByCouponId = GetRedemptionCountsByCouponId();
        var coupons = dbContext.Coupons
            .ToArray()
            .Where(coupon => request.IsActive is null || coupon.IsActive == request.IsActive.Value)
            .Where(coupon => request.EffectiveAt is null || IsEffectiveAt(coupon, request.EffectiveAt.Value))
            .Where(coupon => normalizedSearch is null || MatchesSearch(coupon, normalizedSearch))
            .OrderByDescending(coupon => coupon.CreatedAt)
            .ThenBy(coupon => coupon.Code)
            .ToArray();

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var page = new PagedResult<AdminCouponDto>(
            coupons
                .Skip(request.Skip)
                .Take(pageSize)
                .Select(coupon => ToDto(
                    coupon,
                    productTargetsByCouponId[coupon.Id],
                    redemptionCountsByCouponId.GetValueOrDefault(coupon.Id)))
                .ToArray(),
            pageNumber,
            pageSize,
            coupons.Length);

        return Result<PagedResult<AdminCouponDto>>.Success(page);
    }

    public Task<Result<AdminCouponDto>> GetCouponByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var coupon = dbContext.Coupons.FirstOrDefault(existing => existing.Id == id);
        if (coupon is null)
        {
            return Task.FromResult(Result<AdminCouponDto>.NotFound("Coupon was not found."));
        }

        return Task.FromResult(Result<AdminCouponDto>.Success(ToDto(
            coupon,
            GetProductTargets(coupon.Id),
            GetRedemptionCount(coupon.Id))));
    }

    public async Task<Result<AdminCouponDto>> CreateCouponAsync(
        CreateCouponRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminCouponDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var normalizedCode = NormalizeCode(request.Code);
        if (CodeExists(normalizedCode))
        {
            return Result<AdminCouponDto>.Conflict("Coupon code already exists.");
        }

        var targetValidation = ValidateProductTargets(request.ProductTargetIds);
        if (targetValidation.Length > 0)
        {
            return Result<AdminCouponDto>.Validation(targetValidation);
        }

        try
        {
            var coupon = new Coupon(
                Guid.NewGuid(),
                normalizedCode,
                request.Name,
                request.Description,
                request.DiscountType,
                request.DiscountValue,
                request.MaxDiscountAmount,
                request.MinimumSubtotal,
                request.StartsAt,
                request.EndsAt,
                request.UsageLimit,
                request.IsActive);

            dbContext.Add(coupon);
            var targetIds = NormalizeProductTargetIds(request.ProductTargetIds);
            foreach (var productId in targetIds)
            {
                dbContext.Add(new CouponProductTarget(Guid.NewGuid(), coupon.Id, productId));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminCouponDto>.Success(ToDto(coupon, targetIds, redemptionCount: 0));
        }
        catch (DomainException exception)
        {
            return Result<AdminCouponDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminCouponDto>> UpdateCouponAsync(
        Guid id,
        UpdateCouponRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminCouponDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var coupon = dbContext.Coupons.FirstOrDefault(existing => existing.Id == id);
        if (coupon is null)
        {
            return Result<AdminCouponDto>.NotFound("Coupon was not found.");
        }

        var normalizedCode = NormalizeCode(request.Code);
        if (CodeExists(normalizedCode, id))
        {
            return Result<AdminCouponDto>.Conflict("Coupon code already exists.");
        }

        var targetValidation = ValidateProductTargets(request.ProductTargetIds);
        if (targetValidation.Length > 0)
        {
            return Result<AdminCouponDto>.Validation(targetValidation);
        }

        try
        {
            coupon.UpdateDetails(
                normalizedCode,
                request.Name,
                request.Description,
                request.DiscountType,
                request.DiscountValue,
                request.MaxDiscountAmount,
                request.MinimumSubtotal,
                request.StartsAt,
                request.EndsAt,
                request.UsageLimit);

            if (request.IsActive)
            {
                coupon.Activate();
            }
            else
            {
                coupon.Deactivate();
            }

            ReplaceProductTargets(coupon.Id, request.ProductTargetIds);
            dbContext.Update(coupon);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminCouponDto>.Success(ToDto(
                coupon,
                NormalizeProductTargetIds(request.ProductTargetIds),
                GetRedemptionCount(coupon.Id)));
        }
        catch (DomainException exception)
        {
            return Result<AdminCouponDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminCouponDto>> UpdateStatusAsync(
        Guid id,
        UpdateCouponStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await statusValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminCouponDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var coupon = dbContext.Coupons.FirstOrDefault(existing => existing.Id == id);
        if (coupon is null)
        {
            return Result<AdminCouponDto>.NotFound("Coupon was not found.");
        }

        if (request.IsActive)
        {
            coupon.Activate();
        }
        else
        {
            coupon.Deactivate();
        }

        dbContext.Update(coupon);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminCouponDto>.Success(ToDto(
            coupon,
            GetProductTargets(coupon.Id),
            GetRedemptionCount(coupon.Id)));
    }

    public async Task<Result<AdminCouponDto>> DeleteCouponAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var coupon = dbContext.Coupons.FirstOrDefault(existing => existing.Id == id);
        if (coupon is null)
        {
            return Result<AdminCouponDto>.NotFound("Coupon was not found.");
        }

        var productTargets = GetProductTargets(id);
        var redemptionCount = GetRedemptionCount(id);
        var hasUsageHistory = redemptionCount > 0 || dbContext.Orders.Any(order => order.CouponId == id);
        if (hasUsageHistory)
        {
            coupon.Deactivate();
            dbContext.Update(coupon);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminCouponDto>.Success(ToDto(coupon, productTargets, redemptionCount));
        }

        var dto = ToDto(coupon, productTargets, redemptionCount);
        foreach (var target in dbContext.CouponProductTargets.Where(target => target.CouponId == id).ToArray())
        {
            dbContext.Remove(target);
        }

        dbContext.Remove(coupon);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminCouponDto>.Success(dto);
    }

    private void ReplaceProductTargets(Guid couponId, IReadOnlyCollection<Guid> productTargetIds)
    {
        foreach (var existingTarget in dbContext.CouponProductTargets.Where(target => target.CouponId == couponId).ToArray())
        {
            dbContext.Remove(existingTarget);
        }

        foreach (var productId in NormalizeProductTargetIds(productTargetIds))
        {
            dbContext.Add(new CouponProductTarget(Guid.NewGuid(), couponId, productId));
        }
    }

    private string[] ValidateProductTargets(IReadOnlyCollection<Guid> productTargetIds)
    {
        var normalizedProductTargetIds = NormalizeProductTargetIds(productTargetIds);
        var missingIds = normalizedProductTargetIds
            .Where(productId => !dbContext.Products.Any(product => product.Id == productId))
            .ToArray();

        return missingIds.Length == 0 ? [] : ["Coupon target product does not exist."];
    }

    private ILookup<Guid, Guid> GetProductTargetsByCouponId()
    {
        return dbContext.CouponProductTargets
            .OrderBy(target => target.ProductId)
            .ToLookup(target => target.CouponId, target => target.ProductId);
    }

    private Dictionary<Guid, int> GetRedemptionCountsByCouponId()
    {
        return dbContext.CouponRedemptions
            .GroupBy(redemption => redemption.CouponId)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private Guid[] GetProductTargets(Guid couponId)
    {
        return dbContext.CouponProductTargets
            .Where(target => target.CouponId == couponId)
            .Select(target => target.ProductId)
            .OrderBy(productId => productId)
            .ToArray();
    }

    private int GetRedemptionCount(Guid couponId)
    {
        return dbContext.CouponRedemptions.Count(redemption => redemption.CouponId == couponId);
    }

    private bool CodeExists(string code, Guid? excludedCouponId = null)
    {
        return dbContext.Coupons.Any(coupon =>
            coupon.Code == code &&
            (excludedCouponId == null || coupon.Id != excludedCouponId.Value));
    }

    private static AdminCouponDto ToDto(
        Coupon coupon,
        IEnumerable<Guid> productTargetIds,
        int redemptionCount)
    {
        return new AdminCouponDto(
            coupon.Id,
            coupon.Code,
            coupon.Name,
            coupon.Description,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.MaxDiscountAmount,
            coupon.MinimumSubtotal,
            coupon.StartsAt,
            coupon.EndsAt,
            coupon.UsageLimit,
            coupon.UsedCount,
            redemptionCount,
            coupon.IsActive,
            productTargetIds.OrderBy(productId => productId).ToArray(),
            coupon.CreatedAt,
            coupon.UpdatedAt);
    }

    private static bool IsEffectiveAt(Coupon coupon, DateTimeOffset effectiveAt)
    {
        return (coupon.StartsAt is null || coupon.StartsAt.Value <= effectiveAt) &&
            (coupon.EndsAt is null || coupon.EndsAt.Value >= effectiveAt);
    }

    private static bool MatchesSearch(Coupon coupon, string search)
    {
        return coupon.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            coupon.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static Guid[] NormalizeProductTargetIds(IEnumerable<Guid> productTargetIds)
    {
        return productTargetIds.Distinct().OrderBy(productId => productId).ToArray();
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
