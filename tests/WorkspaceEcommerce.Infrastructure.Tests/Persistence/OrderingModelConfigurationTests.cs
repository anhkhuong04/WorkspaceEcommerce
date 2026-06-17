using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class OrderingModelConfigurationTests
{
    [Theory]
    [InlineData(typeof(Order), "orders")]
    [InlineData(typeof(OrderItem), "order_items")]
    [InlineData(typeof(OrderStatusHistory), "order_status_history")]
    public void OrderingEntities_AreMappedToOrderingSchema(Type entityType, string tableName)
    {
        var metadata = GetEntityType(entityType);

        Assert.Equal("ordering", metadata.GetSchema());
        Assert.Equal(tableName, metadata.GetTableName());
    }

    [Fact]
    public void OrderCode_HasUniqueIndex()
    {
        var metadata = GetEntityType(typeof(Order));
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == nameof(Order.OrderCode));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_orders_order_code", index.GetDatabaseName());
    }

    [Fact]
    public void OrderCustomerId_HasIndex()
    {
        var metadata = GetEntityType(typeof(Order));
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == nameof(Order.CustomerId));

        Assert.False(index.IsUnique);
        Assert.Equal("ix_orders_customer_id", index.GetDatabaseName());
    }

    [Theory]
    [InlineData(nameof(Order.Subtotal))]
    [InlineData(nameof(Order.ShippingFee))]
    [InlineData(nameof(Order.DiscountAmount))]
    [InlineData(nameof(Order.TotalAmount))]
    public void OrderMoneyFields_HaveDecimalPrecision(string propertyName)
    {
        var property = GetEntityType(typeof(Order)).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(18,2)", property.GetColumnType());
    }

    [Fact]
    public void OrderItemUnitPrice_HasDecimalPrecision()
    {
        var property = GetEntityType(typeof(OrderItem)).FindProperty(nameof(OrderItem.UnitPrice));

        Assert.NotNull(property);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(18,2)", property.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(Order.Status), "status")]
    [InlineData(nameof(Order.PaymentMethod), "payment_method")]
    public void OrderEnums_AreStoredAsStrings(string propertyName, string columnName)
    {
        var property = GetEntityType(typeof(Order)).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("character varying(50)", property.GetColumnType());
        Assert.Equal(columnName, property.GetColumnName());
    }

    [Theory]
    [InlineData(typeof(OrderItem), typeof(Order), nameof(OrderItem.OrderId), DeleteBehavior.Cascade)]
    [InlineData(typeof(OrderItem), typeof(ProductVariant), nameof(OrderItem.ProductVariantId), DeleteBehavior.Restrict)]
    [InlineData(typeof(OrderStatusHistory), typeof(Order), nameof(OrderStatusHistory.OrderId), DeleteBehavior.Cascade)]
    [InlineData(typeof(Order), typeof(Customer), nameof(Order.CustomerId), DeleteBehavior.Restrict)]
    public void OrderingRelationships_HaveExpectedDeleteBehavior(
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

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;

        using var dbContext = new AppDbContext(options);

        return dbContext.Model;
    }
}
