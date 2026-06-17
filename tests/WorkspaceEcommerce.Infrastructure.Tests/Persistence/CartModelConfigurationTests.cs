using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Persistence;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class CartModelConfigurationTests
{
    [Theory]
    [InlineData(typeof(CartAggregate), "carts")]
    [InlineData(typeof(CartItem), "cart_items")]
    public void CartEntities_AreMappedToCartSchema(Type entityType, string tableName)
    {
        var metadata = GetEntityType(entityType);

        Assert.Equal("cart", metadata.GetSchema());
        Assert.Equal(tableName, metadata.GetTableName());
    }

    [Fact]
    public void CartItemVariantIdentity_HasUniqueIndexWithinCart()
    {
        var metadata = GetEntityType(typeof(CartItem));
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(CartItem.CartId), nameof(CartItem.ProductVariantId)]));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_cart_items_cart_id_product_variant_id", index.GetDatabaseName());
    }

    [Theory]
    [InlineData(typeof(CartAggregate), nameof(CartAggregate.SessionId), "ix_carts_session_id")]
    [InlineData(typeof(CartAggregate), nameof(CartAggregate.CustomerId), "ix_carts_customer_id")]
    [InlineData(typeof(CartItem), nameof(CartItem.CartId), "ix_cart_items_cart_id")]
    [InlineData(typeof(CartItem), nameof(CartItem.ProductVariantId), "ix_cart_items_product_variant_id")]
    public void CartLookupFields_HaveIndexes(Type entityType, string propertyName, string databaseName)
    {
        var metadata = GetEntityType(entityType);
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == propertyName);

        Assert.Equal(databaseName, index.GetDatabaseName());
    }

    [Fact]
    public void CartItemUnitPriceSnapshot_HasDecimalPrecision()
    {
        var property = GetEntityType(typeof(CartItem)).FindProperty(nameof(CartItem.UnitPriceSnapshot));

        Assert.NotNull(property);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(18,2)", property.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(CartItem), typeof(CartAggregate), nameof(CartItem.CartId), DeleteBehavior.Cascade)]
    [InlineData(typeof(CartItem), typeof(ProductVariant), nameof(CartItem.ProductVariantId), DeleteBehavior.Restrict)]
    [InlineData(typeof(CartAggregate), typeof(Customer), nameof(CartAggregate.CustomerId), DeleteBehavior.Restrict)]
    public void CartRelationships_HaveExpectedDeleteBehavior(
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
