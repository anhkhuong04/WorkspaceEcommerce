using Microsoft.Extensions.Logging.Abstractions;
using WorkspaceEcommerce.Application.Modules.Operations;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Operations;

public sealed class OutboxOperationsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReplayCustomerEmailAsync_CreatesANewPendingCommandAndRetainsTheDeadLetter()
    {
        var dbContext = new FakeAppDbContext();
        var message = CreateDeadLetterEmail();
        dbContext.Seed(message);
        var service = CreateService(dbContext);

        var result = await service.ReplayCustomerEmailAsync(message.Id, "admin-session-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, dbContext.CustomerEmailOutboxMessages.Count());
        Assert.Equal(CustomerEmailOutboxStatus.DeadLetter, message.Status);
        var replay = dbContext.CustomerEmailOutboxMessages.Single(candidate => candidate.Id != message.Id);
        Assert.Equal(CustomerEmailOutboxStatus.Pending, replay.Status);
        Assert.Equal(message.ProtectedPayload, replay.ProtectedPayload);
    }

    [Fact]
    public async Task ReplayShipmentCommandAsync_CreatesANewActiveCommandAndRetainsTheDeadLetter()
    {
        var dbContext = new FakeAppDbContext();
        var command = CreateDeadLetterShipment();
        dbContext.Seed(command);
        var service = CreateService(dbContext);

        var result = await service.ReplayShipmentCommandAsync(command.Id, "admin-session-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, dbContext.ShipmentCommandOutbox.Count());
        Assert.Equal(ShipmentCommandStatus.DeadLetter, command.Status);
        var replay = dbContext.ShipmentCommandOutbox.Single(candidate => candidate.Id != command.Id);
        Assert.Equal(ShipmentCommandStatus.Pending, replay.Status);
        Assert.Equal(command.OrderId, replay.OrderId);
        Assert.Equal(command.CommandType, replay.CommandType);
    }

    [Fact]
    public async Task GetSummaryAsync_ReportsDueRetryAndDeadLetterCountsWithoutPayloadData()
    {
        var dbContext = new FakeAppDbContext();
        var retryingEmail = new CustomerEmailOutboxMessage(
            Guid.NewGuid(),
            "customer@example.com",
            "Account notice",
            "protected-payload",
            Now.AddMinutes(-10));
        var emailLease = Guid.NewGuid();
        retryingEmail.Claim("worker", emailLease, Now.AddMinutes(-10), Now.AddMinutes(-9));
        retryingEmail.ScheduleRetry(emailLease, "Delivery failed (SmtpException).", Now.AddMinutes(-1));
        dbContext.Seed(retryingEmail, CreateDeadLetterEmail());
        dbContext.Seed(new ShipmentCommandOutbox(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ShipmentCommandType.Create,
            reason: null,
            Now.AddMinutes(-5)));
        dbContext.Seed(CreateDeadLetterShipment());
        var service = CreateService(dbContext);

        var result = await service.GetSummaryAsync();

        Assert.True(result.IsSuccess);
        var email = result.Value!.Queues.Single(queue => queue.Outbox == "customer-email");
        var shipment = result.Value.Queues.Single(queue => queue.Outbox == "shipment-command");
        Assert.Equal(1, email.DueCount);
        Assert.Equal(1, email.RetryCount);
        Assert.Equal(1, email.DeadLetterCount);
        Assert.Equal(1, shipment.DueCount);
        Assert.Equal(1, shipment.DeadLetterCount);
    }

    private static OutboxOperationsService CreateService(FakeAppDbContext dbContext) => new(
        dbContext,
        new StubTimeProvider(Now),
        NullLogger<OutboxOperationsService>.Instance);

    private static CustomerEmailOutboxMessage CreateDeadLetterEmail()
    {
        var message = new CustomerEmailOutboxMessage(
            Guid.NewGuid(),
            "customer@example.com",
            "Account notice",
            "protected-payload",
            Now.AddMinutes(-20));
        var lease = Guid.NewGuid();
        message.Claim("worker", lease, Now.AddMinutes(-20), Now.AddMinutes(-19));
        message.DeadLetter(lease, "Delivery failed (SmtpException).", Now.AddMinutes(-19));
        return message;
    }

    private static ShipmentCommandOutbox CreateDeadLetterShipment()
    {
        var command = new ShipmentCommandOutbox(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ShipmentCommandType.Cancel,
            "Provider conflict",
            Now.AddMinutes(-20));
        var lease = Guid.NewGuid();
        command.Claim("worker", lease, Now.AddMinutes(-20), Now.AddMinutes(-19));
        command.DeadLetter(lease, "Shipment provider rejected the cancellation.", "Conflict", Now.AddMinutes(-19));
        return command;
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
