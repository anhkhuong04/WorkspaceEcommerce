using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Tests.Modules.Customers;

public sealed class CustomerEmailOutboxMessageTests
{
    [Fact]
    public void ClaimedMessage_RequiresTheCurrentLeaseTokenAndCanBeReclaimedAfterExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage(now);
        var firstLease = Guid.NewGuid();
        var secondLease = Guid.NewGuid();

        message.Claim("worker-a", firstLease, now, now.AddMinutes(1));

        Assert.False(message.IsDueAt(now.AddSeconds(30)));
        Assert.Throws<DomainException>(() => message.MarkSent(Guid.NewGuid(), now.AddSeconds(30)));
        Assert.Throws<DomainException>(() => message.Claim("worker-b", secondLease, now.AddSeconds(30), now.AddMinutes(2)));

        message.Claim("worker-b", secondLease, now.AddMinutes(1), now.AddMinutes(2));
        message.MarkSent(secondLease, now.AddMinutes(1));

        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.SentAt);
        Assert.Null(message.LeaseToken);
        Assert.Null(message.LeaseExpiresAt);
    }

    [Fact]
    public void DeadLetter_StopsFutureClaimsAndRetainsOnlyTheStableFailureCategory()
    {
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage(now);
        var lease = Guid.NewGuid();

        message.Claim("worker-a", lease, now, now.AddMinutes(1));
        message.DeadLetter(lease, "Delivery failed (SmtpException).", now.AddSeconds(1));

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("Delivery failed (SmtpException).", message.LastError);
        Assert.NotNull(message.DeadLetteredAt);
        Assert.False(message.IsDueAt(now.AddDays(1)));
        Assert.Throws<DomainException>(() => message.Claim("worker-b", Guid.NewGuid(), now.AddDays(1), now.AddDays(1).AddMinutes(1)));
    }

    [Fact]
    public void Retry_ReleasesTheLeaseForTheNextScheduledAttempt()
    {
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage(now);
        var lease = Guid.NewGuid();

        message.Claim("worker-a", lease, now, now.AddMinutes(1));
        message.ScheduleRetry(lease, "Delivery failed (SmtpException).", now.AddMinutes(2));

        Assert.Equal(1, message.AttemptCount);
        Assert.Null(message.LeaseToken);
        Assert.Null(message.LeaseExpiresAt);
        Assert.False(message.IsDueAt(now.AddMinutes(1)));
        Assert.True(message.IsDueAt(now.AddMinutes(2)));
    }

    private static CustomerEmailOutboxMessage CreateMessage(DateTimeOffset now) => new(
        Guid.NewGuid(),
        "customer@example.com",
        "Account notice",
        "protected-payload",
        now);
}
