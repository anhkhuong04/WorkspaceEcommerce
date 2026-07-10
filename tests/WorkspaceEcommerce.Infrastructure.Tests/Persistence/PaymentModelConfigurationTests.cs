using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class PaymentModelConfigurationTests
{
    [Fact]
    public void PaymentTransaction_IsMappedToPaymentsSchema()
    {
        var metadata = GetEntityType(typeof(PaymentTransaction));

        Assert.Equal("payments", metadata.GetSchema());
        Assert.Equal("payment_transactions", metadata.GetTableName());
    }

    [Fact]
    public void PaymentTransactionTxnRef_HasUniqueIndex()
    {
        var metadata = GetEntityType(typeof(PaymentTransaction));
        var index = GetIndex(metadata, nameof(PaymentTransaction.TxnRef));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_payment_transactions_txn_ref", index.GetDatabaseName());
    }

    [Fact]
    public void PaymentTransactionOrderId_HasIndex()
    {
        var metadata = GetEntityType(typeof(PaymentTransaction));
        var index = GetIndex(metadata, nameof(PaymentTransaction.OrderId));

        Assert.False(index.IsUnique);
        Assert.Equal("ix_payment_transactions_order_id", index.GetDatabaseName());
    }

    [Fact]
    public void PaymentTransactionAmount_HasDecimalPrecision()
    {
        var property = GetEntityType(typeof(PaymentTransaction)).FindProperty(nameof(PaymentTransaction.Amount));

        Assert.NotNull(property);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(18,2)", property.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(PaymentTransaction.Provider), "provider")]
    [InlineData(nameof(PaymentTransaction.Status), "status")]
    public void PaymentTransactionEnums_AreStoredAsStrings(string propertyName, string columnName)
    {
        var property = GetEntityType(typeof(PaymentTransaction)).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal("character varying(50)", property.GetColumnType());
        Assert.Equal(columnName, property.GetColumnName());
    }

    [Fact]
    public void PaymentTransactionRawResponse_IsMappedAsJsonb()
    {
        var property = GetEntityType(typeof(PaymentTransaction)).FindProperty(nameof(PaymentTransaction.RawResponse));

        Assert.NotNull(property);
        Assert.Equal("jsonb", property.GetColumnType());
        Assert.Equal("raw_response", property.GetColumnName());
    }

    [Fact]
    public void PaymentTransactionOrderRelationship_HasRestrictDeleteBehavior()
    {
        var metadata = GetEntityType(typeof(PaymentTransaction));
        var foreignKey = metadata.GetForeignKeys().Single(candidate =>
            candidate.PrincipalEntityType.ClrType == typeof(Order)
            && candidate.Properties.Any(property => property.Name == nameof(PaymentTransaction.OrderId)));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
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
