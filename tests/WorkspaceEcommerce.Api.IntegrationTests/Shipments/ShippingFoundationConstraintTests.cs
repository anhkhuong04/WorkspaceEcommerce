using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class ShippingFoundationConstraintTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task Database_RejectsSecondPublishedPlanAndDuplicateProvinceInSamePlan()
    {
        await fixture.ResetDatabaseAsync();
        var plan = Plan();
        await fixture.SeedAsync(context => { context.Add(plan); return Task.CompletedTask; });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.SeedAsync(context =>
        {
            context.Add(Plan());
            return Task.CompletedTask;
        }));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);

        var zoneError = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.SeedAsync(context =>
        {
            context.Add(new ShippingRateZone(Guid.NewGuid(), plan.Id, "other", "Other", 1000, 10000, 1000, 5000, null, ["01"]));
            return Task.CompletedTask;
        }));
        Assert.Equal("ux_shipping_zone_plan_province", Assert.IsType<PostgresException>(zoneError.InnerException).ConstraintName);
    }

    [Fact]
    public async Task TwoIndependentContexts_CannotConsumeSameQuoteForDifferentOrders()
    {
        await fixture.ResetDatabaseAsync();
        var now = DateTimeOffset.UtcNow;
        var firstOrder = Order("QUOTE-A");
        var secondOrder = Order("QUOTE-B");
        var quote = new ShippingQuote(Guid.NewGuid(), ShippingMethod.MiniLogistics, new string('a', 64),
            new string('b', 64), 10000, 1000, "{}", null, now, now.AddMinutes(10));
        await fixture.SeedAsync(context =>
        {
            context.AddRange(firstOrder, secondOrder, quote);
            return Task.CompletedTask;
        });
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSaved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stale = fixture.ExecuteDbAsync(async context =>
        {
            var read = await context.Set<ShippingQuote>().SingleAsync();
            loaded.SetResult();
            await firstSaved.Task;
            read.Consume(secondOrder.Id, now.AddSeconds(1));
            return await Assert.ThrowsAsync<PersistenceConcurrencyException>(() => context.SaveChangesAsync());
        });
        await loaded.Task;
        await fixture.ExecuteDbAsync(async context =>
        {
            var read = await context.Set<ShippingQuote>().SingleAsync();
            read.Consume(firstOrder.Id, now.AddSeconds(1));
            return await context.SaveChangesAsync();
        });
        firstSaved.SetResult();
        await stale;
        Assert.Equal(firstOrder.Id, await fixture.ExecuteDbAsync(context =>
            context.Set<ShippingQuote>().Select(x => x.OrderId).SingleAsync()));
    }

    private static ShippingRatePlan Plan()
    {
        var plan = new ShippingRatePlan(Guid.NewGuid(), "Test plan", "test", "01", DateTimeOffset.UtcNow);
        plan.ReplaceDraft("Test plan", "01", [new ShippingRateZone(Guid.NewGuid(), plan.Id, "zone", "Zone",
            1000, 10000, 1000, 5000, null, ["01"])], DateTimeOffset.UtcNow);
        plan.Publish(DateTimeOffset.UtcNow);
        return plan;
    }
    private static Order Order(string code) => new(Guid.NewGuid(), code, null, "Synthetic", "0900000000", null,
        "Synthetic address", null, PaymentMethod.Cod, "VND", 1);
}
