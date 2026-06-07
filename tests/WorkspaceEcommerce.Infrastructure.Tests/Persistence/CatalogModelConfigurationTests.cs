using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class CatalogModelConfigurationTests
{
    [Theory]
    [InlineData(typeof(Category), "categories")]
    [InlineData(typeof(Product), "products")]
    [InlineData(typeof(ProductVariant), "product_variants")]
    [InlineData(typeof(ProductImage), "product_images")]
    [InlineData(typeof(ProductSpecification), "product_specifications")]
    public void CatalogEntities_AreMappedToCatalogSchema(Type entityType, string tableName)
    {
        var metadata = GetEntityType(entityType);

        Assert.Equal("catalog", metadata.GetSchema());
        Assert.Equal(tableName, metadata.GetTableName());
    }

    [Theory]
    [InlineData(typeof(Category), "Slug", "ux_categories_slug")]
    [InlineData(typeof(Product), "Slug", "ux_products_slug")]
    [InlineData(typeof(ProductVariant), "Sku", "ux_product_variants_sku")]
    public void CatalogIdentifiers_HaveUniqueIndexes(
        Type entityType,
        string propertyName,
        string databaseName)
    {
        var metadata = GetEntityType(entityType);
        var index = GetIndex(metadata, propertyName);

        Assert.True(index.IsUnique);
        Assert.Equal(databaseName, index.GetDatabaseName());
    }

    [Theory]
    [InlineData(nameof(ProductVariant.Price))]
    [InlineData(nameof(ProductVariant.CompareAtPrice))]
    public void ProductVariantMoneyFields_HaveDecimalPrecision(string propertyName)
    {
        var property = GetEntityType(typeof(ProductVariant)).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(18,2)", property.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(Category), typeof(Category), nameof(Category.ParentId), DeleteBehavior.Restrict)]
    [InlineData(typeof(Product), typeof(Category), nameof(Product.CategoryId), DeleteBehavior.Restrict)]
    [InlineData(typeof(ProductVariant), typeof(Product), nameof(ProductVariant.ProductId), DeleteBehavior.Restrict)]
    [InlineData(typeof(ProductImage), typeof(Product), nameof(ProductImage.ProductId), DeleteBehavior.Cascade)]
    [InlineData(typeof(ProductSpecification), typeof(Product), nameof(ProductSpecification.ProductId), DeleteBehavior.Cascade)]
    public void CatalogRelationships_HaveExpectedDeleteBehavior(
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
