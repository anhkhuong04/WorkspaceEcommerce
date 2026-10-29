using System.Text.Json;
using WorkspaceEcommerce.Application.Abstractions.Shipment;

namespace WorkspaceEcommerce.Infrastructure.Shipping;

internal sealed class VietnamShippingAdministrativeCatalog : IShippingAdministrativeCatalog
{
    private readonly Dictionary<string, ShippingProvince> _provinces;
    private readonly Dictionary<(string Province, string Ward), ShippingWard> _wards;

    public VietnamShippingAdministrativeCatalog()
    {
        using var stream = typeof(VietnamShippingAdministrativeCatalog).Assembly.GetManifestResourceStream(
            "WorkspaceEcommerce.Infrastructure.Shipping.Data.vietnam-administrative-catalog.json")
            ?? throw new InvalidOperationException("The pinned shipping administrative catalog is missing.");
        var data = JsonSerializer.Deserialize<CatalogData>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The shipping administrative catalog is invalid.");
        if (string.IsNullOrWhiteSpace(data.Version) || data.Provinces.Count == 0)
            throw new InvalidOperationException("The catalog needs a version and provinces.");
        Version = data.Version;
        Provinces = data.Provinces.Select(province => province with
        {
            Wards = Array.AsReadOnly(province.Wards.ToArray())
        }).OrderBy(province => province.Code, StringComparer.Ordinal).ToList().AsReadOnly();
        _provinces = Provinces.ToDictionary(province => province.Code, StringComparer.Ordinal);
        _wards = Provinces.SelectMany(province => province.Wards.Select(ward => (province, ward)))
            .ToDictionary(item => (item.province.Code, item.ward.Code), item => item.ward);
        var wardCodes = Provinces.SelectMany(province => province.Wards).Select(ward => ward.Code).ToArray();
        if (wardCodes.Distinct(StringComparer.Ordinal).Count() != wardCodes.Length ||
            Provinces.Any(province => !IsCode(province.Code, 2) || province.Wards.Count == 0 ||
                string.IsNullOrWhiteSpace(province.Name) || province.Wards.Any(ward =>
                    !IsCode(ward.Code, 5) || string.IsNullOrWhiteSpace(ward.Name))))
            throw new InvalidOperationException("Catalog codes/names must be valid, unique and have a known parent.");
    }

    public string Version { get; }
    public IReadOnlyList<ShippingProvince> Provinces { get; }
    public ShippingProvince? FindProvince(string code) => _provinces.GetValueOrDefault(code);
    public ShippingDestination? Resolve(string provinceCode, string wardCode) =>
        _provinces.TryGetValue(provinceCode, out var province) && _wards.TryGetValue((provinceCode, wardCode), out var ward)
            ? new ShippingDestination(Version, province.Code, province.Name, ward.Code, ward.Name)
            : null;

    private static bool IsCode(string value, int length) => value.Length == length && value.All(char.IsAsciiDigit);
    private sealed record CatalogData(string Version, List<ShippingProvince> Provinces);
}
