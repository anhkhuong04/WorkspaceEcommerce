using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class CustomerModelConfigurationTests
{
    [Fact]
    public void CustomerEntity_IsMappedToCustomerSchema()
    {
        var metadata = GetEntityType();

        Assert.Equal("customer", metadata.GetSchema());
        Assert.Equal("customers", metadata.GetTableName());
    }

    [Fact]
    public void CustomerEmail_HasUniqueIndex()
    {
        var metadata = GetEntityType();
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == nameof(Customer.Email));

        Assert.True(index.IsUnique);
        Assert.Equal("ux_customers_email", index.GetDatabaseName());
    }

    [Fact]
    public void CustomerPhoneNumber_HasIndex()
    {
        var metadata = GetEntityType();
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == nameof(Customer.PhoneNumber));

        Assert.False(index.IsUnique);
        Assert.Equal("ix_customers_phone_number", index.GetDatabaseName());
    }

    private static IReadOnlyEntityType GetEntityType()
    {
        var entityType = CreateModel().FindEntityType(typeof(Customer));

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
