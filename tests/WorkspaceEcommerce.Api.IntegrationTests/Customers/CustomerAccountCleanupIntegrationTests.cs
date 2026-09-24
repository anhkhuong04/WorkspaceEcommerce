using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Infrastructure.Notifications;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Api.IntegrationTests.Customers;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CustomerAccountCleanupIntegrationTests(ApiIntegrationTestFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cleanup_TimeBudgetExpires_CommitsOneBoundedBatchAndLeavesFreshRows()
    {
        await fixture.ResetDatabaseAsync();
        var customer = CreateCustomer();
        var expiredTokens = Enumerable.Range(0, 25)
            .Select(index => new CustomerAccountToken(
                Guid.NewGuid(),
                customer.Id,
                CustomerAccountTokenPurpose.EmailVerification,
                $"expired-account-token-{index}-{Guid.NewGuid():N}",
                Now.AddDays(-20),
                Now.AddDays(-10)))
            .ToArray();
        var freshToken = new CustomerAccountToken(
            Guid.NewGuid(),
            customer.Id,
            CustomerAccountTokenPurpose.EmailVerification,
            $"fresh-account-token-{Guid.NewGuid():N}",
            Now,
            Now.AddDays(1));
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Add(customer);
            dbContext.AddRange(expiredTokens);
            dbContext.Add(freshToken);
            return Task.CompletedTask;
        });

        var result = await fixture.ExecuteScopeAsync(async services =>
        {
            var cleanup = new CustomerAccountCleanupService(
                services.GetRequiredService<AppDbContext>(),
                CleanupOptions(batchSize: 10, cycleTimeSeconds: 2),
                new AdvancingTimeProvider(Now));
            return await cleanup.CleanupAsync(CancellationToken.None);
        });

        var remainingAfterBudget = await fixture.ExecuteDbAsync(async dbContext => new
        {
            Expired = await dbContext.CustomerAccountTokens.CountAsync(token => token.ExpiresAt < Now.AddDays(-7)),
            Fresh = await dbContext.CustomerAccountTokens.CountAsync(token => token.Id == freshToken.Id)
        });

        Assert.True(result.LockAcquired);
        Assert.True(result.TimeBudgetExhausted);
        Assert.Equal(10, result.DeletedRows);
        Assert.Equal(1, result.BatchesProcessed);
        Assert.Equal(15, remainingAfterBudget.Expired);
        Assert.Equal(1, remainingAfterBudget.Fresh);

        var nextCycle = await fixture.ExecuteScopeAsync(async services =>
        {
            var cleanup = new CustomerAccountCleanupService(
                services.GetRequiredService<AppDbContext>(),
                CleanupOptions(batchSize: 10, cycleTimeSeconds: 30),
                new FixedTimeProvider(Now));
            return await cleanup.CleanupAsync(CancellationToken.None);
        });
        var remainingAfterNextCycle = await fixture.ExecuteDbAsync(async dbContext => new
        {
            Expired = await dbContext.CustomerAccountTokens.CountAsync(token => token.ExpiresAt < Now.AddDays(-7)),
            Fresh = await dbContext.CustomerAccountTokens.CountAsync(token => token.Id == freshToken.Id)
        });

        Assert.False(nextCycle.TimeBudgetExhausted);
        // The shared test host may enqueue other retention-eligible maintenance rows;
        // the account-token assertions below isolate the backlog under test.
        Assert.True(nextCycle.DeletedRows >= 15);
        Assert.Equal(0, remainingAfterNextCycle.Expired);
        Assert.Equal(1, remainingAfterNextCycle.Fresh);
    }

    [Fact]
    public async Task Cleanup_LargeRefreshBacklog_DrainsInBatchesBeforeFamiliesAndEmitsSafeMetrics()
    {
        await fixture.ResetDatabaseAsync();
        var customer = CreateCustomer();
        var expiredFamilies = new List<CustomerRefreshTokenFamily>();
        var expiredTokens = new List<CustomerRefreshToken>();
        for (var index = 0; index < 25; index++)
        {
            var family = new CustomerRefreshTokenFamily(
                Guid.NewGuid(),
                customer.Id,
                Now.AddDays(-30),
                Now.AddDays(-20));
            expiredFamilies.Add(family);
            expiredTokens.Add(new CustomerRefreshToken(
                Guid.NewGuid(),
                family.Id,
                $"expired-refresh-token-{index}-{Guid.NewGuid():N}",
                Now.AddDays(-30),
                Now.AddDays(-20)));
        }

        var freshFamily = new CustomerRefreshTokenFamily(
            Guid.NewGuid(),
            customer.Id,
            Now,
            Now.AddDays(30));
        var freshToken = new CustomerRefreshToken(
            Guid.NewGuid(),
            freshFamily.Id,
            $"fresh-refresh-token-{Guid.NewGuid():N}",
            Now,
            Now.AddDays(10));
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.Add(customer);
            dbContext.AddRange(expiredFamilies);
            dbContext.AddRange(expiredTokens);
            dbContext.Add(freshFamily);
            dbContext.Add(freshToken);
            return Task.CompletedTask;
        });

        var measurements = new ConcurrentBag<MetricMeasurement>();
        using var listener = CreateMetricsListener(measurements);
        var result = await fixture.ExecuteScopeAsync(async services =>
        {
            var cleanup = new CustomerAccountCleanupService(
                services.GetRequiredService<AppDbContext>(),
                CleanupOptions(batchSize: 7, cycleTimeSeconds: 30),
                new FixedTimeProvider(Now));
            return await cleanup.CleanupAsync(CancellationToken.None);
        });

        var remaining = await fixture.ExecuteDbAsync(async dbContext => new
        {
            ExpiredTokens = await dbContext.CustomerRefreshTokens.CountAsync(token => token.ExpiresAt < Now.AddDays(-7)),
            ExpiredFamilies = await dbContext.CustomerRefreshTokenFamilies.CountAsync(family => family.ExpiresAt < Now.AddDays(-7)),
            FreshTokens = await dbContext.CustomerRefreshTokens.CountAsync(token => token.Id == freshToken.Id),
            FreshFamilies = await dbContext.CustomerRefreshTokenFamilies.CountAsync(family => family.Id == freshFamily.Id)
        });

        Assert.True(result.LockAcquired);
        Assert.False(result.TimeBudgetExhausted);
        Assert.Equal(50, result.DeletedRows);
        Assert.Equal(8, result.BatchesProcessed);
        Assert.Equal(0, remaining.ExpiredTokens);
        Assert.Equal(0, remaining.ExpiredFamilies);
        Assert.Equal(1, remaining.FreshTokens);
        Assert.Equal(1, remaining.FreshFamilies);

        var deletedMeasurements = measurements
            .Where(measurement => measurement.Instrument == "workspaceecommerce.customer_account_cleanup.deleted")
            .ToArray();
        Assert.Equal(50, deletedMeasurements.Sum(measurement => measurement.LongValue));
        Assert.Contains(deletedMeasurements, measurement => measurement.TagValue == "refresh-token");
        Assert.Contains(deletedMeasurements, measurement => measurement.TagValue == "refresh-family");
        Assert.Contains(measurements, measurement =>
            measurement.Instrument == "workspaceecommerce.customer_account_cleanup.duration" &&
            measurement.TagValue == "drained");
        Assert.DoesNotContain(measurements, measurement =>
            measurement.TagKey is not ("dataset" or "outcome"));
    }

    private static MeterListener CreateMetricsListener(ConcurrentBag<MetricMeasurement> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CustomerAccountCleanupMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(CreateMeasurement(instrument.Name, value, null, tags)));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(CreateMeasurement(instrument.Name, 0, value, tags)));
        listener.Start();
        return listener;
    }

    private static MetricMeasurement CreateMeasurement(
        string instrument,
        long longValue,
        double? doubleValue,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tag = tags.Length == 0 ? default : tags[0];
        return new MetricMeasurement(instrument, longValue, doubleValue, tag.Key, tag.Value?.ToString());
    }

    private static Customer CreateCustomer() => Customer.Create(
        Guid.NewGuid(),
        "Cleanup Customer",
        "0900000000",
        $"cleanup-{Guid.NewGuid():N}@example.com",
        "hashed-password");

    private static CustomerAccountLifecycleOptions CleanupOptions(int batchSize, int cycleTimeSeconds) => new()
    {
        CleanupBatchSize = batchSize,
        CleanupCycleTimeSeconds = cycleTimeSeconds,
        ExpiredTokenRetentionDays = 7,
        LoginHistoryRetentionDays = 90
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class AdvancingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private long timestamp = -1;

        public override long TimestampFrequency => 1;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => Interlocked.Increment(ref timestamp);
    }

    private sealed record MetricMeasurement(
        string Instrument,
        long LongValue,
        double? DoubleValue,
        string? TagKey,
        string? TagValue);
}
