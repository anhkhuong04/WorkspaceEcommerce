using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class CouponModelConfigurationTests
{
    [Theory]
    [InlineData(typeof(Coupon), "coupons")]
    [InlineData(typeof(CouponProductTarget), "coupon_product_targets")]
    [InlineData(typeof(CouponRedemption), "coupon_redemptions")]
    public void CouponEntities_AreMappedToPromotionsSchema(Type entityType, string tableName)
    {
        var metadata = GetEntityType(entityType);

        Assert.Equal("promotions", metadata.GetSchema());
        Assert.Equal(tableName, metadata.GetTableName());
    }

    [Fact]
    public void CouponCode_HasUniqueIndex()
    {
        var metadata = GetEntityType(typeof(Coupon));
        var index = GetIndex(metadata, nameof(Coupon.Code));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_coupons_code", index.GetDatabaseName());
    }

    [Fact]
    public void CouponProductTarget_HasUniqueCouponProductIndex()
    {
        var metadata = GetEntityType(typeof(CouponProductTarget));
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 2
            && candidate.Properties[0].Name == nameof(CouponProductTarget.CouponId)
            && candidate.Properties[1].Name == nameof(CouponProductTarget.ProductId));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_coupon_product_targets_coupon_id_product_id", index.GetDatabaseName());
    }

    [Fact]
    public void CouponRedemptionOrderId_HasUniqueIndex()
    {
        var metadata = GetEntityType(typeof(CouponRedemption));
        var index = GetIndex(metadata, nameof(CouponRedemption.OrderId));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_coupon_redemptions_order_id", index.GetDatabaseName());
    }

    [Theory]
    [InlineData(nameof(Coupon.DiscountValue))]
    [InlineData(nameof(Coupon.MaxDiscountAmount))]
    [InlineData(nameof(Coupon.MinimumSubtotal))]
    [InlineData(nameof(CouponRedemption.DiscountAmount))]
    public void CouponMoneyFields_HaveDecimalPrecision(string propertyName)
    {
        var entityType = propertyName == nameof(CouponRedemption.DiscountAmount)
            ? typeof(CouponRedemption)
            : typeof(Coupon);
        var property = GetEntityType(entityType).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(18,2)", property.GetColumnType());
    }

    [Fact]
    public void CouponDiscountType_IsStoredAsString()
    {
        var property = GetEntityType(typeof(Coupon)).FindProperty(nameof(Coupon.DiscountType));

        Assert.NotNull(property);
        Assert.Equal("character varying(50)", property.GetColumnType());
        Assert.Equal("discount_type", property.GetColumnName());
    }

    [Theory]
    [InlineData(typeof(CouponProductTarget), typeof(Coupon), nameof(CouponProductTarget.CouponId), DeleteBehavior.Cascade)]
    [InlineData(typeof(CouponProductTarget), typeof(Product), nameof(CouponProductTarget.ProductId), DeleteBehavior.Restrict)]
    [InlineData(typeof(CouponRedemption), typeof(Coupon), nameof(CouponRedemption.CouponId), DeleteBehavior.Restrict)]
    [InlineData(typeof(CouponRedemption), typeof(Order), nameof(CouponRedemption.OrderId), DeleteBehavior.Restrict)]
    [InlineData(typeof(CouponRedemption), typeof(Customer), nameof(CouponRedemption.CustomerId), DeleteBehavior.Restrict)]
    public void CouponRelationships_HaveExpectedDeleteBehavior(
        Type dependentType,
        Type principalType,
        string foreignKeyPropertyName,
        DeleteBehavior expectedDeleteBehavior)
    {
        var dependent = GetEntityType(dependentType);
        var foreignKey = dependent.GetForeignKeys().Single(candidate =>
            candidate.PrincipalEntityType.ClrType == principalType
            && candidate.Properties.Any(property => property.Name == foreignKeyPropertyName));

        Assert.Equal(expectedDeleteBehavior, foreignKey.DeleteBehavior);
    }

    private static IReadOnlyEntityType GetEntityType(Type clrType)
    {
        var entityType = CreateModel().FindEntityType(clrType);

        Assert.NotNull(entityType);
        return entityType;
    }

    private static IReadOnlyIndex GetIndex(IReadOnlyEntityType entityType, string propertyName)
    {
        return entityType.GetIndexes().Single(index =>
            index.Properties.Count == 1
            && index.Properties[0].Name == propertyName);
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;

        using var dbContext = new AppDbContext(options);

        return dbContext.Model;
    }
}
