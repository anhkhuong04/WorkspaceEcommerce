using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class CustomerEmailOutboxModelConfigurationTests
{
    [Fact]
    public void LeaseMetadata_IsMappedAndLeaseTokenIsAnOptimisticConcurrencyToken()
    {
        var entityType = GetEntityType();

        Assert.Equal("customer", entityType.GetSchema());
        Assert.Equal("email_outbox", entityType.GetTableName());
        Assert.Equal("lease_token", entityType.FindProperty(nameof(CustomerEmailOutboxMessage.LeaseToken))!.GetColumnName());
        Assert.True(entityType.FindProperty(nameof(CustomerEmailOutboxMessage.LeaseToken))!.IsConcurrencyToken);
        Assert.Equal("status", entityType.FindProperty(nameof(CustomerEmailOutboxMessage.Status))!.GetColumnName());
        Assert.Equal("lease_owner", entityType.FindProperty(nameof(CustomerEmailOutboxMessage.LeaseOwner))!.GetColumnName());
        Assert.Equal("lease_expires_at", entityType.FindProperty(nameof(CustomerEmailOutboxMessage.LeaseExpiresAt))!.GetColumnName());
        Assert.Equal("dead_lettered_at", entityType.FindProperty(nameof(CustomerEmailOutboxMessage.DeadLetteredAt))!.GetColumnName());
    }

    [Fact]
    public void ClaimQuery_HasAStableSupportingIndex()
    {
        var entityType = GetEntityType();
        var index = entityType.GetIndexes().Single(candidate =>
            candidate.GetDatabaseName() == "ix_customer_email_outbox_claim");

        Assert.Equal(
            [
                nameof(CustomerEmailOutboxMessage.SentAt),
                nameof(CustomerEmailOutboxMessage.Status),
                nameof(CustomerEmailOutboxMessage.DeadLetteredAt),
                nameof(CustomerEmailOutboxMessage.NextAttemptAt),
                nameof(CustomerEmailOutboxMessage.LeaseExpiresAt),
                nameof(CustomerEmailOutboxMessage.Id)
            ],
            index.Properties.Select(property => property.Name));
    }

    private static IReadOnlyEntityType GetEntityType()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;

        using var dbContext = new AppDbContext(options);
        var entityType = dbContext.Model.FindEntityType(typeof(CustomerEmailOutboxMessage));

        Assert.NotNull(entityType);
        return entityType;
    }
}
