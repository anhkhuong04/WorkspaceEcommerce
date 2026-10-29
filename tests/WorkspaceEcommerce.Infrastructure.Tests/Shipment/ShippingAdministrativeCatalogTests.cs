using WorkspaceEcommerce.Infrastructure.Shipping;

namespace WorkspaceEcommerce.Infrastructure.Tests.Shipment;

public sealed class ShippingAdministrativeCatalogTests
{
    [Fact]
    public void PinnedOfficialCatalog_ResolvesParentChildAndPreservesLeadingZeroes()
    {
        var catalog = new VietnamShippingAdministrativeCatalog();
        Assert.NotEmpty(catalog.Version);
        Assert.NotEmpty(catalog.Provinces);
        Assert.NotNull(catalog.Resolve("01", "00004"));
        Assert.Null(catalog.Resolve("79", "00004"));
        Assert.Null(catalog.Resolve("1", "00004"));
        Assert.Null(catalog.Resolve("01", "4"));
        Assert.NotNull(catalog.Resolve("22", "06970"));
        Assert.Null(catalog.Resolve("22", "06978"));
        Assert.StartsWith("Phường", catalog.Resolve("75", "26116")!.WardName);
        Assert.StartsWith("Thành phố", catalog.FindProvince("75")!.Name);
        Assert.Equal(catalog.Provinces.Sum(province => province.Wards.Count),
            catalog.Provinces.SelectMany(province => province.Wards).Select(ward => ward.Code).Distinct().Count());
    }
}
