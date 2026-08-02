using Microsoft.Extensions.Options;

namespace WorkspaceEcommerce.Infrastructure.Shipment;

internal sealed class MiniLogisticsFailureGate(
    IOptions<MiniLogisticsOptions> options,
    TimeProvider timeProvider)
{
    private readonly object sync = new();
    private int consecutiveFailures;
    private DateTimeOffset? openUntilUtc;

    public void ThrowIfOpen(string operationName)
    {
        lock (sync)
        {
            var now = timeProvider.GetUtcNow();
            if (!openUntilUtc.HasValue || openUntilUtc.Value <= now)
            {
                openUntilUtc = null;
                return;
            }

            throw new HttpRequestException(
                $"MiniLogistics {operationName} is temporarily blocked after repeated provider failures.",
                null,
                System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    public void RecordSuccess()
    {
        lock (sync)
        {
            consecutiveFailures = 0;
            openUntilUtc = null;
        }
    }

    public void RecordTransientFailure()
    {
        lock (sync)
        {
            consecutiveFailures++;
            var threshold = Math.Max(1, options.Value.CircuitBreakerFailureThreshold);
            if (consecutiveFailures < threshold)
            {
                return;
            }

            var breakDuration = TimeSpan.FromSeconds(Math.Max(1, options.Value.CircuitBreakerBreakSeconds));
            openUntilUtc = timeProvider.GetUtcNow().Add(breakDuration);
        }
    }
}
