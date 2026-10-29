using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Shipments;

public sealed class ShippingRateDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PublishedAndRetiredDefinitions_CannotBeEdited()
    {
        var plan = Plan();
        plan.ReplaceDraft("Rates", "01", [Zone(plan.Id, "north", "01")], Now);
        plan.Publish(Now);
        Assert.Equal(3, plan.Revision);
        Assert.Throws<DomainException>(() => plan.ReplaceDraft("Changed", "01", [], Now));
        plan.Retire(Now);
        Assert.Equal(ShippingRatePlanStatus.Retired, plan.Status);
        Assert.Throws<DomainException>(() => plan.ReplaceDraft("Changed", "01", [], Now));
        Assert.Throws<DomainException>(() => plan.Publish(Now));
    }

    [Fact]
    public void DuplicateProvinceAcrossZones_IsRejectedWithoutChangingDraft()
    {
        var plan = Plan();
        Assert.Throws<DomainException>(() => plan.ReplaceDraft("Changed", "01",
            [Zone(plan.Id, "a", "01"), Zone(plan.Id, "b", "01")], Now));
        Assert.Empty(plan.Zones);
        Assert.Equal(1, plan.Revision);
        Assert.Equal("Rates", plan.Name);
    }

    [Theory]
    [InlineData(-1, 1000, 10000, 5000)]
    [InlineData(1000, 0, 10000, 5000)]
    [InlineData(1000, 1000, -1, 5000)]
    [InlineData(1000, 1000, 10000.5, 5000)]
    public void InvalidRates_AreRejected(int baseWeight, int step, decimal fee, decimal stepFee)
    {
        Assert.Throws<DomainException>(() => new ShippingRateZone(Guid.NewGuid(), Guid.NewGuid(),
            "zone", "Zone", baseWeight, fee, step, stepFee, null, ["01"]));
    }

    [Fact]
    public void QuoteConsumption_IsSingleUseAndCopiesFrozenSnapshot()
    {
        var quote = Quote();
        var orderId = Guid.NewGuid();
        quote.Consume(orderId, Now.AddMinutes(1));
        var selection = new OrderShippingSelection(Guid.NewGuid(), quote);
        Assert.Equal(orderId, selection.OrderId);
        Assert.Equal(quote.CustomerCharge, selection.CustomerCharge);
        Assert.Equal(quote.SnapshotJson, selection.SnapshotJson);
        Assert.Throws<DomainException>(() => quote.Consume(Guid.NewGuid(), Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => quote.Revoke(Now.AddMinutes(2)));
    }

    [Fact]
    public void ExpiredOrRevokedQuote_CannotBeConsumed()
    {
        Assert.Throws<DomainException>(() => Quote().Consume(Guid.NewGuid(), Now.AddMinutes(10)));
        var revoked = Quote();
        revoked.Revoke(Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => revoked.Consume(Guid.NewGuid(), Now.AddMinutes(2)));
    }

    [Fact]
    public void LocalShipment_HasRealLocalIdAndUnknownCostWithoutFakeProviderIdentity()
    {
        var id = Guid.NewGuid();
        var shipment = OrderShipment.CreateWorkspace(id, Guid.NewGuid(), "WEC-TEST", Now);
        Assert.Equal(id, shipment.Id);
        Assert.Null(shipment.ProviderShipmentId);
        Assert.Null(shipment.ActualCostAmount);
        Assert.Equal("WorkspaceEC", shipment.Provider);
        Assert.Throws<DomainException>(() => shipment.ApplyProviderState("Delivered", 1000, "VND", Now, Now));
    }

    private static ShippingRatePlan Plan() => new(Guid.NewGuid(), "Rates", "test-v1", "01", Now);
    private static ShippingRateZone Zone(Guid planId, string code, params string[] provinces) =>
        new(Guid.NewGuid(), planId, code, code, 1000, 10000, 1000, 5000, null, provinces);
    private static ShippingQuote Quote() => new(Guid.NewGuid(), ShippingMethod.WorkspaceEC,
        new string('a', 64), new string('b', 64), 10000, 1000, "{\"provinceCode\":\"01\"}",
        null, Now, Now.AddMinutes(10));
}
