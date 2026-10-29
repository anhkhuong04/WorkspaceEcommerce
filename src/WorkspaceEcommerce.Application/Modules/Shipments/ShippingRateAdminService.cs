using System.Text.Json;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

internal sealed partial class ShippingRateAdminService(
    IShippingRateStore store,
    ICatalogReadStore catalogReadStore,
    IShippingAdministrativeCatalog catalog,
    TimeProvider timeProvider,
    IValidator<SaveShippingRatePlanRequest> saveValidator,
    IValidator<ShippingRateActionRequest> actionValidator,
    IValidator<PublishShippingRatePlanRequest> publishValidator,
    IValidator<CloneShippingRatePlanRequest> cloneValidator,
    IValidator<ShippingRatePreviewRequest> previewValidator) : IShippingRateAdminService
{
    public async Task<Result<ShippingRatePlanDto>> CreateAsync(SaveShippingRatePlanRequest request, string actor,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateDefinitionAsync(request, cancellationToken);
        if (request.ExpectedRevision != 0) errors.Add("A new draft must have expectedRevision 0.");
        if (errors.Count > 0) return Result<ShippingRatePlanDto>.Validation(errors);
        return await MutateAsync(actor, async token =>
        {
            var plan = new ShippingRatePlan(Guid.NewGuid(), request.Name, catalog.Version,
                request.OriginProvinceCode, timeProvider.GetUtcNow());
            plan.ReplaceDraft(request.Name, request.OriginProvinceCode, BuildZones(plan.Id, request.Zones), timeProvider.GetUtcNow());
            store.Add(plan);
            Audit(plan, "Created", actor, request.Reason, null);
            await store.SaveChangesAsync(token);
            return Result<ShippingRatePlanDto>.Success(ToDto(plan));
        }, cancellationToken);
    }

    public async Task<Result<ShippingRatePlanDto>> UpdateAsync(Guid id, SaveShippingRatePlanRequest request,
        string actor, CancellationToken cancellationToken)
    {
        var errors = await ValidateDefinitionAsync(request, cancellationToken);
        if (request.ExpectedRevision <= 0) errors.Add("An existing draft requires its expected revision.");
        if (errors.Count > 0) return Result<ShippingRatePlanDto>.Validation(errors);
        return await MutateAsync(actor, async token =>
        {
            var plan = await store.FindAsync(id, token);
            if (plan is null) return NotFound();
            if (plan.Revision != request.ExpectedRevision || plan.Status != ShippingRatePlanStatus.Draft)
                return Conflict();
            var before = ToDto(plan);
            var oldZones = plan.Zones.ToArray();
            var newZones = BuildZones(plan.Id, request.Zones);
            plan.ReplaceDraft(request.Name, request.OriginProvinceCode, newZones, timeProvider.GetUtcNow());
            store.ReplaceZones(oldZones, newZones);
            Audit(plan, "Updated", actor, request.Reason, before);
            await store.SaveChangesAsync(token);
            return Result<ShippingRatePlanDto>.Success(ToDto(plan));
        }, cancellationToken);
    }

    public async Task<Result<ShippingRatePlanDto>> CloneAsync(Guid id, CloneShippingRatePlanRequest request,
        string actor, CancellationToken cancellationToken)
    {
        var validation = await cloneValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<ShippingRatePlanDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        return await MutateAsync(actor, async token =>
        {
            var source = await store.FindAsync(id, token);
            if (source is null) return NotFound();
            if (source.Revision != request.ExpectedRevision) return Conflict();
            if (catalog.FindProvince(source.OriginProvinceCode) is null)
                return Result<ShippingRatePlanDto>.Validation(["Select a current origin before migrating this historical plan."]);
            var clone = new ShippingRatePlan(Guid.NewGuid(), request.Name, catalog.Version,
                source.OriginProvinceCode, timeProvider.GetUtcNow());
            var definitions = source.Zones.Select(zone => ZoneDto(zone) with
            {
                ProvinceCodes = zone.Provinces.Select(item => item.ProvinceCode)
                    .Where(code => catalog.FindProvince(code) is not null).ToArray()
            }).Where(zone => zone.ProvinceCodes.Count > 0).ToArray();
            clone.ReplaceDraft(request.Name, source.OriginProvinceCode, BuildZones(clone.Id, definitions), timeProvider.GetUtcNow());
            store.Add(clone);
            Audit(clone, "Cloned", actor, request.Reason, ToDto(source));
            await store.SaveChangesAsync(token);
            return Result<ShippingRatePlanDto>.Success(ToDto(clone));
        }, cancellationToken);
    }

    public async Task<Result<ShippingRatePlanDto>> PublishAsync(Guid id, PublishShippingRatePlanRequest request,
        string actor, CancellationToken cancellationToken)
    {
        var validation = await publishValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<ShippingRatePlanDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        return await MutateAsync(actor, async token =>
        {
            var plan = await store.FindAsync(id, token);
            if (plan is null) return NotFound();
            if (plan.Revision != request.ExpectedRevision || plan.Status != ShippingRatePlanStatus.Draft)
                return Conflict();
            var coverage = ShippingRateCoverage.Validate(plan, catalog);
            if (!coverage.IsComplete) return Result<ShippingRatePlanDto>.Validation(coverage.Errors);
            var active = await store.Plans.Where(item => item.Status == ShippingRatePlanStatus.Published)
                .FirstOrDefaultAsyncSafe(token);
            if (active?.Id != request.ExpectedActivePlanId || active?.Revision != request.ExpectedActiveRevision)
                return Result<ShippingRatePlanDto>.Conflict("The active rate plan changed. Reload it before publishing a replacement.");
            if (active is not null)
            {
                // Load its full immutable definition so the audit is a complete snapshot.
                active = (await store.FindAsync(active.Id, token))!;
                var previous = ToDto(active);
                active.Retire(timeProvider.GetUtcNow());
                Audit(active, "Replaced", actor, request.Reason, previous);
                // Release the unique Published key before publishing its replacement;
                // both saves and both audits still commit in the same transaction.
                await store.SaveChangesAsync(token);
            }
            var before = ToDto(plan);
            plan.Publish(timeProvider.GetUtcNow());
            Audit(plan, "Published", actor, request.Reason, before);
            await store.SaveChangesAsync(token);
            return Result<ShippingRatePlanDto>.Success(ToDto(plan));
        }, cancellationToken);
    }

    public async Task<Result<ShippingRatePlanDto>> RetireAsync(Guid id, ShippingRateActionRequest request,
        string actor, CancellationToken cancellationToken)
    {
        var validation = await actionValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<ShippingRatePlanDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        return await MutateAsync(actor, async token =>
        {
            var plan = await store.FindAsync(id, token);
            if (plan is null) return NotFound();
            if (plan.Revision != request.ExpectedRevision || plan.Status != ShippingRatePlanStatus.Published)
                return Conflict();
            var before = ToDto(plan);
            plan.Retire(timeProvider.GetUtcNow());
            Audit(plan, "Retired", actor, request.Reason, before);
            await store.SaveChangesAsync(token);
            return Result<ShippingRatePlanDto>.Success(ToDto(plan));
        }, cancellationToken);
    }

    private async Task<List<string>> ValidateDefinitionAsync(SaveShippingRatePlanRequest request, CancellationToken token)
    {
        var validation = await saveValidator.ValidateAsync(request, token);
        var errors = validation.Errors.Select(error => error.ErrorMessage).ToList();
        if (request.CatalogVersion != catalog.Version) errors.Add("Reload the current administrative catalog version.");
        if (catalog.FindProvince(request.OriginProvinceCode ?? string.Empty) is null) errors.Add("Unknown origin province code.");
        if (!validation.IsValid) return errors;
        var unknown = request.Zones.SelectMany(zone => zone.ProvinceCodes).Where(code => catalog.FindProvince(code) is null).Distinct().ToArray();
        if (unknown.Length > 0) errors.Add($"Unknown province codes: {string.Join(", ", unknown)}.");
        return errors;
    }

    private async Task<Result<ShippingRatePlanDto>> MutateAsync(string actor,
        Func<CancellationToken, Task<Result<ShippingRatePlanDto>>> operation, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 256)
            return Result<ShippingRatePlanDto>.Unauthorized("An authenticated administrator session is required.");
        try { return await store.ExecuteConfigurationTransactionAsync(operation, token); }
        catch (DomainException exception) { return Result<ShippingRatePlanDto>.Validation([exception.Message]); }
        catch (PersistenceConcurrencyException) { return Conflict(); }
    }

    private void Audit(ShippingRatePlan plan, string action, string actor, string reason, ShippingRatePlanDto? before) =>
        store.Add(new ShippingRateAudit(Guid.NewGuid(), plan.Id, plan.Revision, action, actor, reason,
            JsonSerializer.Serialize(new { before, after = ToDto(plan) }), timeProvider.GetUtcNow()));

    private static ShippingRateZone[] BuildZones(Guid id, IEnumerable<ShippingRateZoneRequest> zones) =>
        zones.Select(zone => new ShippingRateZone(Guid.NewGuid(), id, zone.Code, zone.Name,
            zone.BaseWeightGrams, zone.BaseFee, zone.StepWeightGrams, zone.StepFee,
            zone.MaximumWeightGrams, zone.ProvinceCodes)).ToArray();
    private static Result<ShippingRatePlanDto> NotFound() => Result<ShippingRatePlanDto>.NotFound("Shipping rate plan was not found.");
    private static Result<ShippingRatePlanDto> Conflict() => Result<ShippingRatePlanDto>.Conflict("The rate plan revision or lifecycle changed. Reload it before retrying.");
}
