using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed record ShippingRateCoverageResult(bool IsComplete, IReadOnlyCollection<string> MissingProvinceCodes,
    IReadOnlyCollection<string> UnknownProvinceCodes, IReadOnlyCollection<string> Errors);

public static class ShippingRateCoverage
{
    public static ShippingRateCoverageResult Validate(ShippingRatePlan plan, IShippingAdministrativeCatalog catalog)
    {
        var expected = catalog.Provinces.Select(province => province.Code).ToHashSet(StringComparer.Ordinal);
        var assigned = plan.Zones.SelectMany(zone => zone.Provinces).Select(item => item.ProvinceCode).ToArray();
        var missing = expected.Except(assigned, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var unknown = assigned.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var errors = new List<string>();
        if (plan.CatalogVersion != catalog.Version) errors.Add("The plan uses a different administrative catalog version; clone against the current catalog.");
        if (!expected.Contains(plan.OriginProvinceCode)) errors.Add("The origin province code is not in the current catalog.");
        if (missing.Length > 0) errors.Add("The plan does not cover every province in the administrative catalog.");
        if (unknown.Length > 0) errors.Add("The plan assigns unknown province codes.");
        if (assigned.Distinct(StringComparer.Ordinal).Count() != assigned.Length)
            errors.Add("A province belongs to multiple zones.");
        if (plan.Zones.Count == 0) errors.Add("The plan has no rate zones.");
        return new ShippingRateCoverageResult(errors.Count == 0, missing, unknown, errors);
    }
}
