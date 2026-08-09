using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
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

        var normalizedSearch = NormalizeOptional(request.Search)?.ToUpperInvariant();
        var query = dbContext.Coupons.AsNoTrackingIfEf();
        if (request.IsActive.HasValue)
        {
            var isActive = request.IsActive.Value;
            query = query.Where(coupon => coupon.IsActive == isActive);
        }

        if (request.EffectiveAt.HasValue)
        {
            var effectiveAt = request.EffectiveAt.Value;
            query = query.Where(coupon =>
                (coupon.StartsAt == null || coupon.StartsAt <= effectiveAt) &&
                (coupon.EndsAt == null || coupon.EndsAt >= effectiveAt));
        }
        if (normalizedSearch is not null)
        {
            query = query.Where(coupon =>
                coupon.Code.ToUpper().Contains(normalizedSearch) ||
                coupon.Name.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var coupons = await query
            .OrderByDescending(coupon => coupon.CreatedAt)
            .ThenBy(coupon => coupon.Code)
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .ToArrayAsyncSafe(cancellationToken);

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var couponIds = coupons.Select(coupon => coupon.Id).ToArray();
        var productTargetsByCouponId = (await dbContext.CouponProductTargets
            .AsNoTrackingIfEf()
            .Where(target => couponIds.Contains(target.CouponId))
            .OrderBy(target => target.ProductId)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(target => target.CouponId, target => target.ProductId);
        var redemptionCountsByCouponId = (await dbContext.CouponRedemptions
            .AsNoTrackingIfEf()
            .Where(redemption => couponIds.Contains(redemption.CouponId))
            .GroupBy(redemption => redemption.CouponId)
            .Select(group => new { CouponId = group.Key, Count = group.Count() })
            .ToArrayAsyncSafe(cancellationToken))
            .ToDictionary(group => group.CouponId, group => group.Count);
        var page = new PagedResult<AdminCouponDto>(
            coupons
                .Select(coupon => ToDto(
                    coupon,
                    productTargetsByCouponId[coupon.Id],
                    redemptionCountsByCouponId.GetValueOrDefault(coupon.Id)))
                .ToArray(),
            pageNumber,
            pageSize,
            totalCount);

        return Result<PagedResult<AdminCouponDto>>.Success(page);
    }

    public async Task<Result<AdminCouponDto>> GetCouponByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var coupon = await dbContext.Coupons
            .AsNoTrackingIfEf()
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (coupon is null)
        {
            return Result<AdminCouponDto>.NotFound("Coupon was not found.");
        }

        return Result<AdminCouponDto>.Success(ToDto(
            coupon,
            await GetProductTargetsAsync(coupon.Id, cancellationToken),
            await GetRedemptionCountAsync(coupon.Id, cancellationToken)));
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
        if (await CodeExistsAsync(normalizedCode, cancellationToken: cancellationToken))
        {
            return Result<AdminCouponDto>.Conflict("Coupon code already exists.");
        }

        var targetValidation = await ValidateProductTargetsAsync(request.ProductTargetIds, cancellationToken);
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

        var coupon = await dbContext.Coupons
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (coupon is null)
        {
            return Result<AdminCouponDto>.NotFound("Coupon was not found.");
        }

        var normalizedCode = NormalizeCode(request.Code);
        if (await CodeExistsAsync(normalizedCode, id, cancellationToken))
        {
            return Result<AdminCouponDto>.Conflict("Coupon code already exists.");
        }

        var targetValidation = await ValidateProductTargetsAsync(request.ProductTargetIds, cancellationToken);
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

            await ReplaceProductTargetsAsync(coupon.Id, request.ProductTargetIds, cancellationToken);
            dbContext.Update(coupon);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminCouponDto>.Success(ToDto(
                coupon,
                NormalizeProductTargetIds(request.ProductTargetIds),
                await GetRedemptionCountAsync(coupon.Id, cancellationToken)));
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

        var coupon = await dbContext.Coupons
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
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
            await GetProductTargetsAsync(coupon.Id, cancellationToken),
            await GetRedemptionCountAsync(coupon.Id, cancellationToken)));
    }

    public async Task<Result<AdminCouponDto>> DeleteCouponAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var coupon = await dbContext.Coupons
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (coupon is null)
        {
            return Result<AdminCouponDto>.NotFound("Coupon was not found.");
        }

        var productTargets = await GetProductTargetsAsync(id, cancellationToken);
        var redemptionCount = await GetRedemptionCountAsync(id, cancellationToken);
        var hasUsageHistory = redemptionCount > 0 || await dbContext.Orders
            .Where(order => order.CouponId == id)
            .AnyAsyncSafe(cancellationToken);
        if (hasUsageHistory)
        {
            coupon.Deactivate();
            dbContext.Update(coupon);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AdminCouponDto>.Success(ToDto(coupon, productTargets, redemptionCount));
        }

        var dto = ToDto(coupon, productTargets, redemptionCount);
        foreach (var target in await dbContext.CouponProductTargets
                     .Where(target => target.CouponId == id)
                     .ToArrayAsyncSafe(cancellationToken))
        {
            dbContext.Remove(target);
        }

        dbContext.Remove(coupon);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminCouponDto>.Success(dto);
    }

    private async Task ReplaceProductTargetsAsync(
        Guid couponId,
        IReadOnlyCollection<Guid> productTargetIds,
        CancellationToken cancellationToken)
    {
        foreach (var existingTarget in await dbContext.CouponProductTargets
                     .Where(target => target.CouponId == couponId)
                     .ToArrayAsyncSafe(cancellationToken))
        {
            dbContext.Remove(existingTarget);
        }

        foreach (var productId in NormalizeProductTargetIds(productTargetIds))
        {
            dbContext.Add(new CouponProductTarget(Guid.NewGuid(), couponId, productId));
        }
    }

    private async Task<string[]> ValidateProductTargetsAsync(
        IReadOnlyCollection<Guid> productTargetIds,
        CancellationToken cancellationToken)
    {
        var normalizedProductTargetIds = NormalizeProductTargetIds(productTargetIds);
        var foundIds = await dbContext.Products
            .AsNoTrackingIfEf()
            .Where(product => normalizedProductTargetIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToArrayAsyncSafe(cancellationToken);

        return foundIds.Length == normalizedProductTargetIds.Length ? [] : ["Coupon target product does not exist."];
    }

    private Task<Guid[]> GetProductTargetsAsync(Guid couponId, CancellationToken cancellationToken)
    {
        return dbContext.CouponProductTargets
            .AsNoTrackingIfEf()
            .Where(target => target.CouponId == couponId)
            .Select(target => target.ProductId)
            .OrderBy(productId => productId)
            .ToArrayAsyncSafe(cancellationToken);
    }

    private Task<int> GetRedemptionCountAsync(Guid couponId, CancellationToken cancellationToken)
    {
        return dbContext.CouponRedemptions
            .Where(redemption => redemption.CouponId == couponId)
            .CountAsyncSafe(cancellationToken);
    }

    private Task<bool> CodeExistsAsync(
        string code,
        Guid? excludedCouponId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Coupons
            .Where(coupon => coupon.Code == code &&
                (excludedCouponId == null || coupon.Id != excludedCouponId.Value))
            .AnyAsyncSafe(cancellationToken);
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
            coupon.CustomerId,
            coupon.Source,
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
