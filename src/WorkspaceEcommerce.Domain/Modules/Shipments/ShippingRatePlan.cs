using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public sealed class ShippingRatePlan : Entity
{
    private readonly List<ShippingRateZone> _zones = [];

    private ShippingRatePlan() { }

    public ShippingRatePlan(Guid id, string name, string catalogVersion, string originProvinceCode,
        DateTimeOffset now) : base(id)
    {
        Name = Required(name, 120, nameof(Name));
        CatalogVersion = Required(catalogVersion, 100, nameof(CatalogVersion));
        OriginProvinceCode = Required(originProvinceCode, 2, nameof(OriginProvinceCode));
        if (now == default) throw new DomainException("A rate plan timestamp is required.");
        CreatedAtUtc = UpdatedAtUtc = now;
        Revision = 1;
    }

    public string Name { get; private set; } = string.Empty;
    public string CatalogVersion { get; private set; } = string.Empty;
    public string OriginProvinceCode { get; private set; } = string.Empty;
    public ShippingRatePlanStatus Status { get; private set; }
    public long Revision { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }
    public IReadOnlyCollection<ShippingRateZone> Zones => _zones.AsReadOnly();

    public void ReplaceDraft(string name, string originProvinceCode,
        IReadOnlyCollection<ShippingRateZone> zones, DateTimeOffset now)
    {
        if (Status != ShippingRatePlanStatus.Draft)
            throw new DomainException("Only draft rate plans can be edited.");
        var newName = Required(name, 120, nameof(Name));
        var origin = Required(originProvinceCode, 2, nameof(OriginProvinceCode));
        if (zones.Count > 100 || zones.Any(zone => zone.RatePlanId != Id) ||
            zones.Select(zone => zone.Code).Distinct(StringComparer.Ordinal).Count() != zones.Count)
            throw new DomainException("Rate zones must have unique codes and belong to this plan (maximum 100).");
        var provinces = zones.SelectMany(zone => zone.Provinces).Select(item => item.ProvinceCode).ToArray();
        if (provinces.Distinct(StringComparer.Ordinal).Count() != provinces.Length)
            throw new DomainException("A province can belong to only one rate zone.");
        Name = newName;
        OriginProvinceCode = origin;
        _zones.Clear();
        _zones.AddRange(zones);
        Touch(now);
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != ShippingRatePlanStatus.Draft || _zones.Count == 0)
            throw new DomainException("Only a nonempty draft can be published.");
        Status = ShippingRatePlanStatus.Published;
        PublishedAtUtc = now;
        Touch(now);
    }

    public void Retire(DateTimeOffset now)
    {
        if (Status != ShippingRatePlanStatus.Published)
            throw new DomainException("Only a published rate plan can be retired.");
        Status = ShippingRatePlanStatus.Retired;
        RetiredAtUtc = now;
        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        if (now == default) throw new DomainException("A rate plan timestamp is required.");
        UpdatedAtUtc = now;
        Revision = checked(Revision + 1);
    }

    internal static string Required(string value, int maximum, string name)
    {
        var normalized = Guard.Required(value, name);
        if (normalized.Length > maximum) throw new DomainException($"{name} exceeds {maximum} characters.");
        return normalized;
    }
}
