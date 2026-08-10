using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Operations;

/// <summary>
/// Admin-only operational view and replay path for terminal background work.
/// Replays create a new durable command; they never edit a failed row in place.
/// </summary>
public interface IOutboxOperationsService
{
    Task<Result<OutboxOperationsSummaryDto>> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<OutboxDeadLetterDto>>> GetDeadLettersAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<Result<OutboxReplayDto>> ReplayCustomerEmailAsync(
        Guid messageId,
        string operatorSessionId,
        CancellationToken cancellationToken = default);

    Task<Result<OutboxReplayDto>> ReplayShipmentCommandAsync(
        Guid commandId,
        string operatorSessionId,
        CancellationToken cancellationToken = default);
}
