using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using WorkspaceEcommerce.Api.Health;

namespace WorkspaceEcommerce.Api.IntegrationTests.Health;

public sealed class ApplicationLivenessHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenApplicationIsRunning_ReturnsHealthy()
    {
        using var lifetime = new TestApplicationLifetime();
        var check = new ApplicationLivenessHealthCheck(lifetime);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApplicationIsStopping_ReturnsUnhealthy()
    {
        using var lifetime = new TestApplicationLifetime();
        var check = new ApplicationLivenessHealthCheck(lifetime);
        lifetime.BeginShutdown();

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication() => BeginShutdown();

        public void BeginShutdown() => _stopping.Cancel();

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
