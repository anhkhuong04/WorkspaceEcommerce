using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

internal sealed partial class ShippingRateAdminService
{
    public ShippingCatalogSummaryDto GetCatalog() => new(catalog.Version,
        catalog.Provinces.Select(province => new ShippingProvinceSummaryDto(province.Code, province.Name)).ToArray());

    public Result<PagedResult<ShippingWard>> GetWards(string provinceCode, int pageNumber, int pageSize)
    {
        if (!ValidPage(pageNumber, pageSize)) return Result<PagedResult<ShippingWard>>.Validation(["Invalid pagination (page 1–100000, size 1–100)."]);
        var province = catalog.FindProvince(provinceCode);
        return province is null ? Result<PagedResult<ShippingWard>>.NotFound("Province was not found.") :
            Result<PagedResult<ShippingWard>>.Success(new PagedResult<ShippingWard>(
                province.Wards.OrderBy(ward => ward.Code, StringComparer.Ordinal).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
                pageNumber, pageSize, province.Wards.Count));
    }

    public async Task<Result<PagedResult<ShippingRatePlanSummaryDto>>> ListAsync(int pageNumber, int pageSize,
        ShippingRatePlanStatus? status, CancellationToken cancellationToken)
    {
        if (!ValidPage(pageNumber, pageSize) || (status.HasValue && !Enum.IsDefined(status.Value)))
            return Result<PagedResult<ShippingRatePlanSummaryDto>>.Validation(["Invalid pagination or rate plan status."]);
        var query = store.Plans.AsNoTrackingIfEf();
        if (status.HasValue) query = query.Where(plan => plan.Status == status.Value);
        var count = await query.CountAsyncSafe(cancellationToken);
        var items = await query.OrderByDescending(plan => plan.CreatedAtUtc).ThenBy(plan => plan.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(plan => new ShippingRatePlanSummaryDto(plan.Id, plan.Name, plan.CatalogVersion, plan.OriginProvinceCode,
                plan.Status, plan.Revision, plan.CreatedAtUtc, plan.UpdatedAtUtc)).ToArrayAsyncSafe(cancellationToken);
        return Result<PagedResult<ShippingRatePlanSummaryDto>>.Success(new(items, pageNumber, pageSize, count));
    }

    public async Task<Result<PagedResult<ShippingWeightIssueDto>>> WeightIssuesAsync(int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!ValidPage(pageNumber, pageSize))
            return Result<PagedResult<ShippingWeightIssueDto>>.Validation(["Invalid pagination."]);
        var query = catalogReadStore.ProductVariants.AsNoTrackingIfEf()
            .Where(variant => variant.WeightKg == null || variant.WeightKg <= 0);
        var count = await query.CountAsyncSafe(cancellationToken);
        var items = await query.OrderBy(variant => variant.Sku).ThenBy(variant => variant.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(variant => new ShippingWeightIssueDto(variant.Id, variant.ProductId,
                variant.Sku, variant.Name, variant.WeightKg, variant.IsActive)).ToArrayAsyncSafe(cancellationToken);
        return Result<PagedResult<ShippingWeightIssueDto>>.Success(new(items, pageNumber, pageSize, count));
    }

    public async Task<Result<ShippingRatePlanDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await store.FindAsync(id, cancellationToken);
        return plan is null ? NotFound() : Result<ShippingRatePlanDto>.Success(ToDto(plan));
    }

    public async Task<Result<ShippingRateCoverageResult>> ValidateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await store.FindAsync(id, cancellationToken);
        return plan is null ? Result<ShippingRateCoverageResult>.NotFound("Shipping rate plan was not found.") :
            Result<ShippingRateCoverageResult>.Success(ShippingRateCoverage.Validate(plan, catalog));
    }

    public async Task<Result<ShippingRatePreviewDto>> PreviewAsync(Guid id, ShippingRatePreviewRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await previewValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<ShippingRatePreviewDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        var plan = await store.FindAsync(id, cancellationToken);
        if (plan is null) return Result<ShippingRatePreviewDto>.NotFound("Shipping rate plan was not found.");
        if (plan.Revision != request.ExpectedRevision || plan.CatalogVersion != catalog.Version)
            return Result<ShippingRatePreviewDto>.Conflict("Reload the plan and current administrative catalog before previewing.");
        var destination = catalog.Resolve(request.ProvinceCode, request.WardCode);
        if (destination is null) return Result<ShippingRatePreviewDto>.Validation(["Ward does not belong to the selected province."]);
        var zone = plan.Zones.SingleOrDefault(zone => zone.Provinces.Any(item => item.ProvinceCode == request.ProvinceCode));
        if (zone is null) return Result<ShippingRatePreviewDto>.Validation(["This draft has no rate for the selected province."]);
        var calculation = WorkspaceShippingRateCalculator.Calculate(zone, request.Items);
        if (calculation.IsFailure) return Result<ShippingRatePreviewDto>.Validation(calculation.Errors);
        return Result<ShippingRatePreviewDto>.Success(new(plan.Id, plan.Revision, plan.Status,
            IsPreview: true, CanPublish: plan.Status == ShippingRatePlanStatus.Draft && ShippingRateCoverage.Validate(plan, catalog).IsComplete,
            zone.Code, destination, calculation.Value!));
    }

    public async Task<Result<PagedResult<ShippingRateAuditDto>>> HistoryAsync(Guid id, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!ValidPage(pageNumber, pageSize)) return Result<PagedResult<ShippingRateAuditDto>>.Validation(["Invalid pagination."]);
        if (!await store.Plans.Where(plan => plan.Id == id).AnyAsyncSafe(cancellationToken))
            return Result<PagedResult<ShippingRateAuditDto>>.NotFound("Shipping rate plan was not found.");
        var query = store.Audits.AsNoTrackingIfEf().Where(audit => audit.RatePlanId == id);
        var count = await query.CountAsyncSafe(cancellationToken);
        var items = await query.OrderByDescending(audit => audit.Revision).ThenBy(audit => audit.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(audit => new ShippingRateAuditDto(audit.Id, audit.RatePlanId, audit.Revision, audit.Action,
                audit.Actor, audit.Reason, audit.OccurredAtUtc, audit.ChangesJson)).ToArrayAsyncSafe(cancellationToken);
        return Result<PagedResult<ShippingRateAuditDto>>.Success(new(items, pageNumber, pageSize, count));
    }

    private static bool ValidPage(int number, int size) => number is >= 1 and <= 100000 && size is >= 1 and <= 100;
    private static ShippingRateZoneRequest ZoneDto(ShippingRateZone zone) => new(zone.Code, zone.Name,
        zone.BaseWeightGrams, zone.BaseFee, zone.StepWeightGrams, zone.StepFee, zone.MaximumWeightGrams,
        zone.Provinces.Select(item => item.ProvinceCode).Order(StringComparer.Ordinal).ToArray());
    private ShippingRatePlanDto ToDto(ShippingRatePlan plan) => new(plan.Id, plan.Name, plan.CatalogVersion,
        plan.OriginProvinceCode, plan.Status, plan.Revision, plan.CreatedAtUtc, plan.UpdatedAtUtc,
        plan.PublishedAtUtc, plan.RetiredAtUtc, plan.Zones.OrderBy(zone => zone.Code, StringComparer.Ordinal).Select(ZoneDto).ToArray(),
        ShippingRateCoverage.Validate(plan, catalog));
}
