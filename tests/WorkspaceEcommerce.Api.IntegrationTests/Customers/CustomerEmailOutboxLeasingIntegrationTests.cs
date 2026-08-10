using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Notifications;

namespace WorkspaceEcommerce.Api.IntegrationTests.Customers;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CustomerEmailOutboxLeasingIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task ClaimDueMessagesAsync_ExcludesAnActiveLeaseAndReclaimsOnlyAfterItExpires()
    {
        await fixture.ResetDatabaseAsync();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var messageId = Guid.NewGuid();
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Add(new CustomerEmailOutboxMessage(
                messageId,
                "customer@example.com",
                "Account notice",
                "protected-payload",
                now));
            return Task.CompletedTask;
        });

        var firstLease = Guid.NewGuid();
        var firstClaim = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.ClaimDueMessagesAsync(
                "integration-worker-a",
                TimeSpan.FromMinutes(1),
                firstLease,
                batchSize: 10,
                cancellationToken: CancellationToken.None));
        var blockedClaim = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.ClaimDueMessagesAsync(
                "integration-worker-b",
                TimeSpan.FromMinutes(1),
                Guid.NewGuid(),
                batchSize: 10,
                cancellationToken: CancellationToken.None));

        await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE customer.email_outbox
                SET lease_expires_at = clock_timestamp() - INTERVAL '1 second'
                WHERE id = {messageId}
                """));

        var secondLease = Guid.NewGuid();
        var reclaimed = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.ClaimDueMessagesAsync(
                "integration-worker-c",
                TimeSpan.FromMinutes(1),
                secondLease,
                batchSize: 10,
                cancellationToken: CancellationToken.None));
        var persisted = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.CustomerEmailOutboxMessages.SingleAsync(message => message.Id == messageId));

        Assert.Equal([messageId], firstClaim.Select(message => message.Id));
        Assert.Empty(blockedClaim);
        Assert.Equal([messageId], reclaimed.Select(message => message.Id));
        Assert.Equal(secondLease, persisted.LeaseToken);
        Assert.Equal(CustomerEmailOutboxStatus.Leased, persisted.Status);
        Assert.Equal("integration-worker-c", persisted.LeaseOwner);
        Assert.NotNull(persisted.LeaseExpiresAt);
    }
}
