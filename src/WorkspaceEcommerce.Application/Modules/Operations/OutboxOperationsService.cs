using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Operations;

internal sealed class OutboxOperationsService(
    IAppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<OutboxOperationsService> logger) : IOutboxOperationsService
{
    private const int MaxDeadLetterPageSize = 100;

    public async Task<Result<OutboxOperationsSummaryDto>> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var email = await GetEmailSnapshotAsync(now, cancellationToken);
        var shipment = await GetShipmentSnapshotAsync(now, cancellationToken);

        return Result<OutboxOperationsSummaryDto>.Success(new OutboxOperationsSummaryDto(
            now,
            [email, shipment]));
    }

    public async Task<Result<IReadOnlyCollection<OutboxDeadLetterDto>>> GetDeadLettersAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(limit, 1, MaxDeadLetterPageSize);
        var emailDeadLetters = await dbContext.CustomerEmailOutboxMessages
            .AsNoTrackingIfEf()
            .Where(message => message.Status == CustomerEmailOutboxStatus.DeadLetter &&
                message.DeadLetteredAt != null)
            .OrderByDescending(message => message.DeadLetteredAt)
            .ThenByDescending(message => message.Id)
            .Take(pageSize)
            .Select(message => new OutboxDeadLetterDto(
                "customer-email",
                message.Id,
                null,
                null,
                message.AttemptCount,
                null,
                message.LastError,
                message.DeadLetteredAt!.Value))
            .ToArrayAsyncSafe(cancellationToken);
        var shipmentDeadLetters = await dbContext.ShipmentCommandOutbox
            .AsNoTrackingIfEf()
            .Where(command => command.Status == ShipmentCommandStatus.DeadLetter &&
                command.DeadLetteredAtUtc != null)
            .OrderByDescending(command => command.DeadLetteredAtUtc)
            .ThenByDescending(command => command.Id)
            .Take(pageSize)
            .Select(command => new OutboxDeadLetterDto(
                "shipment-command",
                command.Id,
                command.OrderId,
                command.CommandType.ToString(),
                command.AttemptCount,
                command.LastErrorCategory,
                command.LastError,
                command.DeadLetteredAtUtc!.Value))
            .ToArrayAsyncSafe(cancellationToken);

        IReadOnlyCollection<OutboxDeadLetterDto> result = emailDeadLetters
            .Concat(shipmentDeadLetters)
            .OrderByDescending(item => item.DeadLetteredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(pageSize)
            .ToArray();
        return Result<IReadOnlyCollection<OutboxDeadLetterDto>>.Success(result);
    }

    public async Task<Result<OutboxReplayDto>> ReplayCustomerEmailAsync(
        Guid messageId,
        string operatorSessionId,
        CancellationToken cancellationToken = default)
    {
        if (messageId == Guid.Empty)
        {
            return Result<OutboxReplayDto>.Validation(["An email outbox message id is required."]);
        }

        if (string.IsNullOrWhiteSpace(operatorSessionId))
        {
            return Result<OutboxReplayDto>.Validation(["An authenticated administrator session id is required for replay."]);
        }

        var message = await dbContext.CustomerEmailOutboxMessages
            .Where(candidate => candidate.Id == messageId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (message is null)
        {
            return Result<OutboxReplayDto>.NotFound("Email outbox message was not found.");
        }

        if (message.Status != CustomerEmailOutboxStatus.DeadLetter)
        {
            return Result<OutboxReplayDto>.Conflict("Only dead-lettered email messages can be replayed.");
        }

        var replay = new CustomerEmailOutboxMessage(
            Guid.NewGuid(),
            message.RecipientEmail,
            message.Subject,
            message.ProtectedPayload,
            timeProvider.GetUtcNow());
        dbContext.Add(replay);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning(
            "Admin replayed customer-email outbox message {SourceMessageId} as {ReplayMessageId}; operator session {OperatorSessionId}",
            message.Id,
            replay.Id,
            operatorSessionId);

        return Result<OutboxReplayDto>.Success(new OutboxReplayDto(
            "customer-email",
            message.Id,
            Queued: true));
    }

    public async Task<Result<OutboxReplayDto>> ReplayShipmentCommandAsync(
        Guid commandId,
        string operatorSessionId,
        CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty)
        {
            return Result<OutboxReplayDto>.Validation(["A shipment command id is required."]);
        }

        if (string.IsNullOrWhiteSpace(operatorSessionId))
        {
            return Result<OutboxReplayDto>.Validation(["An authenticated administrator session id is required for replay."]);
        }

        var command = await dbContext.ShipmentCommandOutbox
            .Where(candidate => candidate.Id == commandId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (command is null)
        {
            return Result<OutboxReplayDto>.NotFound("Shipment command was not found.");
        }

        if (command.Status != ShipmentCommandStatus.DeadLetter)
        {
            return Result<OutboxReplayDto>.Conflict("Only dead-lettered shipment commands can be replayed.");
        }

        var queued = await dbContext.TryEnqueueShipmentCommandAsync(
            command.OrderId,
            command.CommandType,
            command.Reason,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (!queued)
        {
            return Result<OutboxReplayDto>.Conflict("An active shipment command of this type is already queued for the order.");
        }

        logger.LogWarning(
            "Admin replayed shipment outbox command {SourceCommandId}; operator session {OperatorSessionId}",
            command.Id,
            operatorSessionId);
        return Result<OutboxReplayDto>.Success(new OutboxReplayDto(
            "shipment-command",
            command.Id,
            Queued: true));
    }

    private async Task<OutboxQueueSnapshotDto> GetEmailSnapshotAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var messages = dbContext.CustomerEmailOutboxMessages.AsNoTrackingIfEf();
        var active = messages.Where(message =>
            message.Status == CustomerEmailOutboxStatus.Pending ||
            message.Status == CustomerEmailOutboxStatus.Leased);
        var due = await active
            .Where(message => message.NextAttemptAt <= now &&
                (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now))
            .CountAsyncSafe(cancellationToken);
        var leased = await messages
            .Where(message => message.Status == CustomerEmailOutboxStatus.Leased &&
                message.LeaseExpiresAt != null && message.LeaseExpiresAt > now)
            .CountAsyncSafe(cancellationToken);
        var retrying = await active
            .Where(message => message.AttemptCount > 0)
            .CountAsyncSafe(cancellationToken);
        var deadLetters = await messages
            .Where(message => message.Status == CustomerEmailOutboxStatus.DeadLetter)
            .CountAsyncSafe(cancellationToken);
        var oldest = await active
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(message => (DateTimeOffset?)message.CreatedAt)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return ToSnapshot("customer-email", due, leased, retrying, deadLetters, oldest, now);
    }

    private async Task<OutboxQueueSnapshotDto> GetShipmentSnapshotAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var commands = dbContext.ShipmentCommandOutbox.AsNoTrackingIfEf();
        var active = commands.Where(command =>
            command.Status == ShipmentCommandStatus.Pending ||
            command.Status == ShipmentCommandStatus.Leased);
        var due = await active
            .Where(command => command.NextAttemptAtUtc <= now &&
                (command.LeaseExpiresAtUtc == null || command.LeaseExpiresAtUtc <= now))
            .CountAsyncSafe(cancellationToken);
        var leased = await commands
            .Where(command => command.Status == ShipmentCommandStatus.Leased &&
                command.LeaseExpiresAtUtc != null && command.LeaseExpiresAtUtc > now)
            .CountAsyncSafe(cancellationToken);
        var retrying = await active
            .Where(command => command.AttemptCount > 0)
            .CountAsyncSafe(cancellationToken);
        var deadLetters = await commands
            .Where(command => command.Status == ShipmentCommandStatus.DeadLetter)
            .CountAsyncSafe(cancellationToken);
        var oldest = await active
            .OrderBy(command => command.CreatedAtUtc)
            .ThenBy(command => command.Id)
            .Select(command => (DateTimeOffset?)command.CreatedAtUtc)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return ToSnapshot("shipment-command", due, leased, retrying, deadLetters, oldest, now);
    }

    private static OutboxQueueSnapshotDto ToSnapshot(
        string outbox,
        int due,
        int leased,
        int retrying,
        int deadLetters,
        DateTimeOffset? oldest,
        DateTimeOffset now) =>
        new(
            outbox,
            due,
            leased,
            retrying,
            deadLetters,
            oldest,
            oldest is null ? null : Math.Max(0d, (now - oldest.Value).TotalSeconds));
}
