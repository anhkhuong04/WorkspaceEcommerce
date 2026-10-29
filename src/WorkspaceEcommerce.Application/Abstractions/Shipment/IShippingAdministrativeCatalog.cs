namespace WorkspaceEcommerce.Application.Abstractions.Shipment;

public interface IShippingAdministrativeCatalog
{
    string Version { get; }
    IReadOnlyList<ShippingProvince> Provinces { get; }
    ShippingProvince? FindProvince(string code);
    ShippingDestination? Resolve(string provinceCode, string wardCode);
}

public sealed record ShippingWard(string Code, string Name);
public sealed record ShippingProvince(string Code, string Name, IReadOnlyList<ShippingWard> Wards);
public sealed record ShippingDestination(string CatalogVersion, string ProvinceCode, string ProvinceName,
    string WardCode, string WardName);
