using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed record ShippingRateZoneRequest(string Code, string Name, int BaseWeightGrams, decimal BaseFee,
    int StepWeightGrams, decimal StepFee, int? MaximumWeightGrams, IReadOnlyCollection<string> ProvinceCodes);
public sealed class SaveShippingRatePlanRequest
{
    public string Name { get; init; } = string.Empty;
    public string CatalogVersion { get; init; } = string.Empty;
    public string OriginProvinceCode { get; init; } = string.Empty;
    public IReadOnlyCollection<ShippingRateZoneRequest> Zones { get; init; } = [];
    public long ExpectedRevision { get; init; }
    public string Reason { get; init; } = string.Empty;
}
public sealed record ShippingRateActionRequest(long ExpectedRevision, string Reason);
public sealed record PublishShippingRatePlanRequest(long ExpectedRevision, string Reason,
    Guid? ExpectedActivePlanId = null, long? ExpectedActiveRevision = null);
public sealed record CloneShippingRatePlanRequest(long ExpectedRevision, string Name, string Reason);
public sealed record ShippingRatePreviewRequest(string ProvinceCode, string WardCode, long ExpectedRevision,
    IReadOnlyCollection<ShippingWeightLine> Items);
public sealed record ShippingRatePlanSummaryDto(Guid Id, string Name, string CatalogVersion,
    string OriginProvinceCode, ShippingRatePlanStatus Status, long Revision, DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
public sealed record ShippingRatePlanDto(Guid Id, string Name, string CatalogVersion, string OriginProvinceCode,
    ShippingRatePlanStatus Status, long Revision, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc, DateTimeOffset? RetiredAtUtc, IReadOnlyCollection<ShippingRateZoneRequest> Zones,
    ShippingRateCoverageResult Coverage);
public sealed record ShippingRatePreviewDto(Guid RatePlanId, long Revision, ShippingRatePlanStatus Status,
    bool IsPreview, bool CanPublish, string ZoneCode, ShippingDestination Destination, ShippingRateCalculation Calculation);
public sealed record ShippingRateAuditDto(Guid Id, Guid RatePlanId, long Revision, string Action, string Actor,
    string Reason, DateTimeOffset OccurredAtUtc, string ChangesJson);
public sealed record ShippingProvinceSummaryDto(string Code, string Name);
public sealed record ShippingCatalogSummaryDto(string Version, IReadOnlyCollection<ShippingProvinceSummaryDto> Provinces);
public sealed record ShippingWeightIssueDto(Guid ProductVariantId, Guid ProductId, string Sku,
    string Name, decimal? WeightKg, bool IsActive);
