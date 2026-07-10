using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class LoyaltyModelConfigurationTests
{
    [Theory]
    [InlineData(typeof(CustomerLoyaltyAccount), "customer_loyalty_accounts")]
    [InlineData(typeof(LoyaltyTransaction), "loyalty_transactions")]
    [InlineData(typeof(LoyaltyTier), "loyalty_tiers")]
    public void LoyaltyEntities_AreMappedToLoyaltySchema(Type entityType, string tableName)
    {
        var metadata = GetEntityType(entityType);

        Assert.Equal("loyalty", metadata.GetSchema());
        Assert.Equal(tableName, metadata.GetTableName());
    }

    [Fact]
    public void CustomerLoyaltyAccountCustomerId_HasUniqueIndex()
    {
        var metadata = GetEntityType(typeof(CustomerLoyaltyAccount));
        var index = GetIndex(metadata, nameof(CustomerLoyaltyAccount.CustomerId));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_customer_loyalty_accounts_customer_id", index.GetDatabaseName());
    }

    [Fact]
    public void CustomerLoyaltyAccount_UsesXminConcurrencyToken()
    {
        var metadata = GetEntityType(typeof(CustomerLoyaltyAccount));
        var xmin = metadata.FindProperty("Version");

        Assert.NotNull(xmin);
        Assert.True(xmin.IsConcurrencyToken);
        Assert.Equal("xmin", xmin.GetColumnName());
        Assert.Equal("xid", xmin.GetColumnType());
    }

    [Fact]
    public void LoyaltyTransactionEarnOrder_HasPartialUniqueIndex()
    {
        var metadata = GetEntityType(typeof(LoyaltyTransaction));
        var index = GetIndex(metadata, nameof(LoyaltyTransaction.OrderId));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_loyalty_transactions_earn_order", index.GetDatabaseName());
        Assert.Equal("\"type\" = 'Earn' AND order_id IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void LoyaltyTierType_HasUniqueIndex()
    {
        var metadata = GetEntityType(typeof(LoyaltyTier));
        var index = GetIndex(metadata, nameof(LoyaltyTier.Type));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_loyalty_tiers_type", index.GetDatabaseName());
    }

    [Theory]
    [InlineData(typeof(CustomerLoyaltyAccount), typeof(Customer), nameof(CustomerLoyaltyAccount.CustomerId), DeleteBehavior.Restrict)]
    [InlineData(typeof(LoyaltyTransaction), typeof(CustomerLoyaltyAccount), nameof(LoyaltyTransaction.CustomerLoyaltyAccountId), DeleteBehavior.Cascade)]
    [InlineData(typeof(LoyaltyTransaction), typeof(Order), nameof(LoyaltyTransaction.OrderId), DeleteBehavior.Restrict)]
    [InlineData(typeof(LoyaltyTransaction), typeof(Coupon), nameof(LoyaltyTransaction.VoucherId), DeleteBehavior.Restrict)]
    public void LoyaltyRelationships_HaveExpectedDeleteBehavior(
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

    [Fact]
    public void LoyaltyTierSeedData_HasDefaultTiers()
    {
        var metadata = GetDesignTimeEntityType(typeof(LoyaltyTier));
        var seedData = metadata.GetSeedData();

        Assert.Equal(4, seedData.Count());
        Assert.Contains(seedData, row =>
            row[nameof(LoyaltyTier.Type)]?.Equals(LoyaltyTierType.Bronze) == true
            && row[nameof(LoyaltyTier.MinTotalPointsEarned)]?.Equals(0) == true);
        Assert.Contains(seedData, row =>
            row[nameof(LoyaltyTier.Type)]?.Equals(LoyaltyTierType.Silver) == true
            && row[nameof(LoyaltyTier.MinTotalPointsEarned)]?.Equals(500) == true);
        Assert.Contains(seedData, row =>
            row[nameof(LoyaltyTier.Type)]?.Equals(LoyaltyTierType.Gold) == true
            && row[nameof(LoyaltyTier.MinTotalPointsEarned)]?.Equals(2000) == true);
        Assert.Contains(seedData, row =>
            row[nameof(LoyaltyTier.Type)]?.Equals(LoyaltyTierType.Platinum) == true
            && row[nameof(LoyaltyTier.MinTotalPointsEarned)]?.Equals(5000) == true);
    }

    [Fact]
    public void LoyaltyTierDiscountPercent_HasDecimalPrecision()
    {
        var property = GetEntityType(typeof(LoyaltyTier)).FindProperty(nameof(LoyaltyTier.DiscountPercent));

        Assert.NotNull(property);
        Assert.Equal(5, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(5,2)", property.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(CustomerLoyaltyAccount), nameof(CustomerLoyaltyAccount.CurrentTier), "current_tier")]
    [InlineData(typeof(LoyaltyTransaction), nameof(LoyaltyTransaction.Type), "type")]
    [InlineData(typeof(LoyaltyTier), nameof(LoyaltyTier.Type), "type")]
    public void LoyaltyEnums_AreStoredAsStrings(Type entityType, string propertyName, string columnName)
    {
        var property = GetEntityType(entityType).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("character varying(50)", property.GetColumnType());
        Assert.Equal(columnName, property.GetColumnName());
    }

    private static IReadOnlyEntityType GetEntityType(Type clrType)
    {
        var entityType = CreateModel().FindEntityType(clrType);

        Assert.NotNull(entityType);
        return entityType;
    }

    private static IReadOnlyEntityType GetDesignTimeEntityType(Type clrType)
    {
        var entityType = CreateDesignTimeModel().FindEntityType(clrType);

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

    private static IModel CreateDesignTimeModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;

        using var dbContext = new AppDbContext(options);

        return dbContext.GetService<IDesignTimeModel>().Model;
    }
}
