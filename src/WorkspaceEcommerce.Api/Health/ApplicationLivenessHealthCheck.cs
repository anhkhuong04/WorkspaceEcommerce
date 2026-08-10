using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WorkspaceEcommerce.Api.Health;

/// <summary>
/// Liveness intentionally has no dependency checks. It becomes unhealthy only
/// once graceful shutdown has started so the load balancer can stop routing
/// new work while in-flight requests drain.
/// </summary>
internal sealed class ApplicationLivenessHealthCheck(
    IHostApplicationLifetime applicationLifetime) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = applicationLifetime.ApplicationStopping.IsCancellationRequested
            ? HealthCheckResult.Unhealthy("Application shutdown is in progress.")
            : HealthCheckResult.Healthy("Application process is accepting work.");

        return Task.FromResult(result);
    }
}
