using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Shipments;

public sealed class ShippingRateCoverageTests
{
    [Fact]
    public void Coverage_RequiresAllCatalogProvincesWithoutUnknownCodes()
    {
        var catalog = new Catalog();
        var plan = new ShippingRatePlan(Guid.NewGuid(), "Test", catalog.Version, "01", DateTimeOffset.UtcNow);
        plan.ReplaceDraft("Test", "01", [Zone(plan.Id, "01", "99")], DateTimeOffset.UtcNow);
        var invalid = ShippingRateCoverage.Validate(plan, catalog);
        Assert.False(invalid.IsComplete);
        Assert.Equal(["79"], invalid.MissingProvinceCodes);
        Assert.Equal(["99"], invalid.UnknownProvinceCodes);
        plan.ReplaceDraft("Test", "01", [Zone(plan.Id, "01", "79")], DateTimeOffset.UtcNow);
        Assert.True(ShippingRateCoverage.Validate(plan, catalog).IsComplete);
    }

    [Fact]
    public void OldVersionOrUnknownOrigin_IsNotPublishableEvenWhenCoverageIsComplete()
    {
        var plan = new ShippingRatePlan(Guid.NewGuid(), "Test", "older", "99", DateTimeOffset.UtcNow);
        plan.ReplaceDraft("Test", "99", [Zone(plan.Id, "01", "79")], DateTimeOffset.UtcNow);
        var result = ShippingRateCoverage.Validate(plan, new Catalog());
        Assert.False(result.IsComplete);
        Assert.Empty(result.MissingProvinceCodes);
        Assert.Equal(2, result.Errors.Count);
    }

    private static ShippingRateZone Zone(Guid planId, params string[] codes) =>
        new(Guid.NewGuid(), planId, "zone", "Zone", 1000, 10000, 1000, 5000, null, codes);

    private sealed class Catalog : IShippingAdministrativeCatalog
    {
        public string Version => "test-v1";
        public IReadOnlyList<ShippingProvince> Provinces => [new("01", "North", []), new("79", "South", [])];
        public ShippingProvince? FindProvince(string code) => Provinces.FirstOrDefault(province => province.Code == code);
        public ShippingDestination? Resolve(string provinceCode, string wardCode) => null;
    }
}
