using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Coupons;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Application.Tests.Modules.Coupons;

public sealed class AdminCouponServiceTests
{
    [Fact]
    public async Task CreateCouponAsync_ValidRequest_CreatesNormalizedCouponAndTargets()
    {
        var product = CreateProduct();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(product);
        var service = CreateService(dbContext);

        var result = await service.CreateCouponAsync(new CreateCouponRequest
        {
            Code = " summer10 ",
            Name = "Summer 10",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 10m,
            ProductTargetIds = [product.Id]
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SUMMER10", result.Value.Code);
        Assert.Equal([product.Id], result.Value.ProductTargetIds);
        Assert.Single(dbContext.Coupons);
        Assert.Single(dbContext.CouponProductTargets);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCouponAsync_DuplicateCode_ReturnsConflict()
    {
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateCoupon("SUMMER10"));
        var service = CreateService(dbContext);

        var result = await service.CreateCouponAsync(new CreateCouponRequest
        {
            Code = "summer10",
            Name = "Summer duplicate",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 10m
        });

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Coupon code already exists.", result.Errors);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCouponAsync_MissingProductTarget_ReturnsValidation()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.CreateCouponAsync(new CreateCouponRequest
        {
            Code = "TARGET10",
            Name = "Target 10",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 10m,
            ProductTargetIds = [Guid.NewGuid()]
        });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Coupon target product does not exist.", result.Errors);
    }

    [Fact]
    public async Task GetCouponsAsync_FiltersBySearchActiveAndEffectiveDate()
    {
        var now = DateTimeOffset.UtcNow;
        var active = CreateCoupon("ACTIVE10", name: "Active coupon", startsAt: now.AddDays(-1), endsAt: now.AddDays(1));
        var inactive = CreateCoupon("INACTIVE10", name: "Inactive coupon", startsAt: now.AddDays(-1), endsAt: now.AddDays(1), isActive: false);
        var expired = CreateCoupon("EXPIRED10", name: "Active expired", startsAt: now.AddDays(-2), endsAt: now.AddDays(-1));
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(active, inactive, expired);
        var service = CreateService(dbContext);

        var result = await service.GetCouponsAsync(new AdminCouponListRequest
        {
            Search = "active",
            IsActive = true,
            EffectiveAt = now
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(active.Id, item.Id);
    }

    [Fact]
    public async Task GetCouponByIdAsync_ExistingCoupon_ReturnsTargetsAndRedemptionCount()
    {
        var coupon = CreateCoupon("SAVE10");
        var productId = Guid.NewGuid();
        var target = new CouponProductTarget(Guid.NewGuid(), coupon.Id, productId);
        var redemption = new CouponRedemption(Guid.NewGuid(), coupon.Id, Guid.NewGuid(), null, "0900000000", coupon.Code, 10m);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(coupon);
        dbContext.Seed(target);
        dbContext.Seed(redemption);
        var service = CreateService(dbContext);

        var result = await service.GetCouponByIdAsync(coupon.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal([productId], result.Value.ProductTargetIds);
        Assert.Equal(1, result.Value.RedemptionCount);
    }

    [Fact]
    public async Task UpdateCouponAsync_ExistingCoupon_ReplacesTargetsAndDeactivates()
    {
        var coupon = CreateCoupon("OLD10");
        var oldTarget = new CouponProductTarget(Guid.NewGuid(), coupon.Id, Guid.NewGuid());
        var product = CreateProduct();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(coupon);
        dbContext.Seed(oldTarget);
        dbContext.Seed(product);
        var service = CreateService(dbContext);

        var result = await service.UpdateCouponAsync(coupon.Id, new UpdateCouponRequest
        {
            Code = "NEW20",
            Name = "New 20",
            DiscountType = CouponDiscountType.FixedAmount,
            DiscountValue = 20m,
            IsActive = false,
            ProductTargetIds = [product.Id]
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("NEW20", result.Value.Code);
        Assert.False(result.Value.IsActive);
        Assert.Equal([product.Id], result.Value.ProductTargetIds);
        Assert.Equal([product.Id], dbContext.CouponProductTargets.Select(target => target.ProductId).ToArray());
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateCouponAsync_UsageLimitLowerThanUsedCount_ReturnsValidationWithoutMutatingCoupon()
    {
        var coupon = CreateCoupon("SAVE10", usageLimit: 2);
        coupon.ReserveUsage();
        coupon.ReserveUsage();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(coupon);
        var service = CreateService(dbContext);

        var result = await service.UpdateCouponAsync(coupon.Id, new UpdateCouponRequest
        {
            Code = "SAVE20",
            Name = "Save 20",
            DiscountType = CouponDiscountType.Percentage,
            DiscountValue = 20m,
            UsageLimit = 1,
            IsActive = true
        });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal("SAVE10", coupon.Code);
        Assert.Equal(2, coupon.UsageLimit);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ExistingCoupon_ActivatesCoupon()
    {
        var coupon = CreateCoupon("SAVE10", isActive: false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(coupon);
        var service = CreateService(dbContext);

        var result = await service.UpdateStatusAsync(coupon.Id, new UpdateCouponStatusRequest { IsActive = true });

        Assert.True(result.IsSuccess);
        Assert.True(coupon.IsActive);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteCouponAsync_UnusedCoupon_RemovesCouponAndTargets()
    {
        var coupon = CreateCoupon("SAVE10");
        var target = new CouponProductTarget(Guid.NewGuid(), coupon.Id, Guid.NewGuid());
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(coupon);
        dbContext.Seed(target);
        var service = CreateService(dbContext);

        var result = await service.DeleteCouponAsync(coupon.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(dbContext.Coupons);
        Assert.Empty(dbContext.CouponProductTargets);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteCouponAsync_UsedCoupon_DeactivatesInsteadOfDeleting()
    {
        var coupon = CreateCoupon("SAVE10", isActive: true);
        var redemption = new CouponRedemption(Guid.NewGuid(), coupon.Id, Guid.NewGuid(), null, "0900000000", coupon.Code, 10m);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(coupon);
        dbContext.Seed(redemption);
        var service = CreateService(dbContext);

        var result = await service.DeleteCouponAsync(coupon.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(dbContext.Coupons);
        Assert.False(coupon.IsActive);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteCouponAsync_MissingCoupon_ReturnsNotFound()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.DeleteCouponAsync(Guid.NewGuid());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    private static AdminCouponService CreateService(FakeAppDbContext dbContext)
    {
        return new AdminCouponService(
            dbContext,
            new AdminCouponListRequestValidator(),
            new CreateCouponRequestValidator(),
            new UpdateCouponRequestValidator(),
            new UpdateCouponStatusRequestValidator());
    }

    private static Coupon CreateCoupon(
        string code,
        string name = "Coupon",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        int? usageLimit = null,
        bool isActive = true)
    {
        return new Coupon(
            Guid.NewGuid(),
            code,
            name,
            null,
            CouponDiscountType.Percentage,
            10m,
            null,
            null,
            startsAt,
            endsAt,
            usageLimit,
            isActive);
    }

    private static Product CreateProduct()
    {
        return new Product(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new LocalizedText(new Dictionary<string, string> { ["en"] = "Standing Desk" }),
            $"standing-desk-{Guid.NewGuid():N}",
            null);
    }
}
