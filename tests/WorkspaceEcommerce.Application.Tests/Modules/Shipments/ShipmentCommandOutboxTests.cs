using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Shipments;

public sealed class ShipmentCommandOutboxTests
{
    [Fact]
    public void ClaimedCommand_RejectsStaleCompletionAndCanBeReclaimedAfterLeaseExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var command = CreateCommand(now);
        var firstLease = Guid.NewGuid();
        var secondLease = Guid.NewGuid();

        command.Claim("worker-a", firstLease, now, now.AddMinutes(1));

        Assert.False(command.IsDueAt(now.AddSeconds(30)));
        Assert.Throws<DomainException>(() => command.MarkCompleted(Guid.NewGuid(), now.AddSeconds(30)));
        Assert.Throws<DomainException>(() => command.Claim("worker-b", secondLease, now.AddSeconds(30), now.AddMinutes(2)));

        command.Claim("worker-b", secondLease, now.AddMinutes(1), now.AddMinutes(2));
        command.MarkCompleted(secondLease, now.AddMinutes(1));

        Assert.Equal(2, command.AttemptCount);
        Assert.Equal(ShipmentCommandStatus.Completed, command.Status);
        Assert.Null(command.LeaseOwner);
        Assert.Null(command.LeaseToken);
        Assert.Null(command.LeaseExpiresAtUtc);
    }

    [Fact]
    public void DeadLetter_StopsFurtherClaimsAndKeepsTheFailureCategory()
    {
        var now = DateTimeOffset.UtcNow;
        var command = CreateCommand(now);
        var lease = Guid.NewGuid();

        command.Claim("worker-a", lease, now, now.AddMinutes(1));
        command.DeadLetter(lease, "Shipment provider rejected the create request.", "Conflict", now.AddSeconds(1));

        Assert.Equal(ShipmentCommandStatus.DeadLetter, command.Status);
        Assert.Equal("Conflict", command.LastErrorCategory);
        Assert.False(command.IsDueAt(now.AddDays(1)));
        Assert.Throws<DomainException>(() => command.Claim(
            "worker-b",
            Guid.NewGuid(),
            now.AddDays(1),
            now.AddDays(1).AddMinutes(1)));
    }

    private static ShipmentCommandOutbox CreateCommand(DateTimeOffset now) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        ShipmentCommandType.Create,
        reason: null,
        now);
}
