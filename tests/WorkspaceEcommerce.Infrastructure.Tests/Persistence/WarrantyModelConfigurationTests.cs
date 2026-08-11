using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Warranties;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class WarrantyModelConfigurationTests
{
    [Theory]
    [InlineData(typeof(WarrantyPlan), "warranty_plans")]
    [InlineData(typeof(SerializedProductUnit), "serialized_product_units")]
    [InlineData(typeof(WarrantyEntitlement), "warranty_entitlements")]
    [InlineData(typeof(WarrantyAuditEvent), "warranty_audit_events")]
    public void WarrantyEntities_AreMappedToWarrantySchema(Type entityType, string tableName)
    {
        var entity = GetEntity(entityType);
        Assert.Equal("warranty", entity.GetSchema());
        Assert.Equal(tableName, entity.GetTableName());
    }

    [Fact]
    public void SerializedProductUnit_HasUniqueFingerprintIndex()
    {
        var index = GetEntity(typeof(SerializedProductUnit)).GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual([
                nameof(SerializedProductUnit.IdentifierType),
                nameof(SerializedProductUnit.IdentifierKeyVersion),
                nameof(SerializedProductUnit.IdentifierFingerprint)]));
        Assert.True(index.IsUnique);
        Assert.Equal("ux_warranty_units_identifier_fingerprint", index.GetDatabaseName());
    }

    [Fact]
    public void WarrantyEntitlement_HasOneUnitConstraint()
    {
        var index = GetEntity(typeof(WarrantyEntitlement)).GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1 && candidate.Properties[0].Name == nameof(WarrantyEntitlement.SerializedProductUnitId));
        Assert.True(index.IsUnique);
        Assert.Equal("ux_warranty_entitlements_unit_id", index.GetDatabaseName());
    }

    private static IReadOnlyEntityType GetEntity(Type entityType)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;
        using var dbContext = new AppDbContext(options);
        return Assert.IsAssignableFrom<IReadOnlyEntityType>(dbContext.Model.FindEntityType(entityType));
    }
}
