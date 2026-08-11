using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Warranties;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

internal sealed class AdminWarrantyService(
    IAppDbContext dbContext,
    IWarrantyIdentifierProtector identifierProtector,
    ICustomerEmailOutbox customerEmailOutbox,
    WarrantyOptions options,
    TimeProvider timeProvider,
    IValidator<CreateWarrantyPlanRequest> createPlanValidator,
    IValidator<AssignWarrantyPlanRequest> assignPlanValidator,
    IValidator<ImportWarrantyUnitsRequest> importValidator,
    IValidator<AdminWarrantyReasonRequest> reasonValidator,
    IValidator<ReplaceWarrantyRequest> replaceValidator) : IAdminWarrantyService
{
    public async Task<Result<PagedResult<AdminWarrantyPlanDto>>> GetPlansAsync(
        AdminWarrantyPlanListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<PagedResult<AdminWarrantyPlanDto>>.NotFound("Warranty administration is unavailable.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSearch = NormalizeOptional(request.Search)?.ToUpperInvariant();
        var query = dbContext.WarrantyPlans.AsNoTrackingIfEf();
        if (request.IsActive.HasValue)
        {
            query = query.Where(plan => plan.IsActive == request.IsActive.Value);
        }

        if (normalizedSearch is not null)
        {
            query = query.Where(plan => plan.Code.ToUpper().Contains(normalizedSearch) || plan.Name.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var plans = await query
            .OrderByDescending(plan => plan.EffectiveFrom)
            .ThenBy(plan => plan.Code)
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .ToArrayAsyncSafe(cancellationToken);
        var planIds = plans.Select(plan => plan.Id).ToArray();
        var coverages = await dbContext.WarrantyPlanCoverages
            .AsNoTrackingIfEf()
            .Where(coverage => planIds.Contains(coverage.WarrantyPlanId))
            .OrderBy(coverage => coverage.SortOrder)
            .ThenBy(coverage => coverage.ComponentCode)
            .ToArrayAsyncSafe(cancellationToken);
        var coveragesByPlan = coverages.ToLookup(coverage => coverage.WarrantyPlanId);

        return Result<PagedResult<AdminWarrantyPlanDto>>.Success(new PagedResult<AdminWarrantyPlanDto>(
            plans.Select(plan => ToPlanDto(plan, coveragesByPlan[plan.Id])).ToArray(),
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            totalCount));
    }

    public async Task<Result<AdminWarrantyPlanDto>> CreatePlanAsync(
        CreateWarrantyPlanRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyPlanDto>.NotFound("Warranty administration is unavailable.");
        }

        var validation = await createPlanValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdminWarrantyPlanDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.WarrantyPlans.Where(plan => plan.Code == normalizedCode).AnyAsyncSafe(cancellationToken))
        {
            return Result<AdminWarrantyPlanDto>.Conflict("Warranty plan code already exists.");
        }

        try
        {
            var plan = new WarrantyPlan(
                Guid.NewGuid(),
                normalizedCode,
                request.Name,
                request.ActivationWindowDays,
                request.TermsVersion,
                request.EffectiveFrom,
                request.EffectiveTo);
            var coverages = request.Coverages
                .OrderBy(coverage => coverage.SortOrder)
                .ThenBy(coverage => coverage.ComponentCode, StringComparer.OrdinalIgnoreCase)
                .Select(coverage => plan.AddCoverage(
                    Guid.NewGuid(),
                    coverage.ComponentCode,
                    coverage.DisplayName,
                    coverage.DurationMonths,
                    coverage.SortOrder))
                .ToArray();
            dbContext.Add(plan);
            foreach (var coverage in coverages)
            {
                dbContext.Add(coverage);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<AdminWarrantyPlanDto>.Success(ToPlanDto(plan, coverages));
        }
        catch (DomainException exception)
        {
            return Result<AdminWarrantyPlanDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminWarrantyPlanDto>> RetirePlanAsync(
        Guid id,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyPlanDto>.NotFound("Warranty administration is unavailable.");
        }

        var plan = await dbContext.WarrantyPlans
            .Where(candidate => candidate.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (plan is null)
        {
            return Result<AdminWarrantyPlanDto>.NotFound("Warranty plan was not found.");
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            plan.Retire(now);
            dbContext.Update(plan);
            await dbContext.SaveChangesAsync(cancellationToken);
            var coverages = await GetPlanCoveragesAsync(plan.Id, cancellationToken);
            return Result<AdminWarrantyPlanDto>.Success(ToPlanDto(plan, coverages));
        }
        catch (DomainException exception)
        {
            return Result<AdminWarrantyPlanDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result> AssignPlanToVariantAsync(
        Guid variantId,
        AssignWarrantyPlanRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result.NotFound("Warranty administration is unavailable.");
        }

        var validation = await assignPlanValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var variantExists = await dbContext.ProductVariants
            .Where(variant => variant.Id == variantId)
            .AnyAsyncSafe(cancellationToken);
        if (!variantExists)
        {
            return Result.NotFound("Product variant was not found.");
        }

        var plan = await dbContext.WarrantyPlans
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == request.WarrantyPlanId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (plan is null)
        {
            return Result.NotFound("Warranty plan was not found.");
        }

        if (await dbContext.ProductVariantWarrantyPlans.Where(mapping =>
                mapping.ProductVariantId == variantId && mapping.EffectiveFrom == request.EffectiveFrom).AnyAsyncSafe(cancellationToken))
        {
            return Result.Conflict("A warranty plan assignment already exists for this variant and effective date.");
        }

        try
        {
            dbContext.Add(new ProductVariantWarrantyPlan(
                Guid.NewGuid(),
                variantId,
                plan.Id,
                request.EffectiveFrom,
                request.EffectiveTo));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException exception)
        {
            return Result.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminWarrantyImportResultDto>> ImportUnitsAsync(
        ImportWarrantyUnitsRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyImportResultDto>.NotFound("Warranty administration is unavailable.");
        }

        if (request.Rows.Length > options.MaxImportRows)
        {
            return Result<AdminWarrantyImportResultDto>.Validation(["The warranty import exceeds the configured row limit."]);
        }

        var validation = await importValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdminWarrantyImportResultDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var parsedRows = request.Rows
            .OrderBy(row => row.RowNumber)
            .Select(row => ParseImportRow(row))
            .ToArray();
        var now = timeProvider.GetUtcNow();
        var normalizedSkus = parsedRows.Where(row => row.Identifier is not null)
            .Select(row => row.Sku)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var variants = await dbContext.ProductVariants
            .AsNoTrackingIfEf()
            .Where(variant => normalizedSkus.Contains(variant.Sku))
            .Select(variant => new { variant.Id, variant.Sku })
            .ToArrayAsyncSafe(cancellationToken);
        var variantsBySku = variants.ToDictionary(variant => variant.Sku, StringComparer.Ordinal);
        var variantIds = variants.Select(variant => variant.Id).ToArray();
        var effectivePlanVariantIds = (await dbContext.ProductVariantWarrantyPlans
            .AsNoTrackingIfEf()
            .Where(mapping => variantIds.Contains(mapping.ProductVariantId) &&
                mapping.EffectiveFrom <= now &&
                (mapping.EffectiveTo == null || mapping.EffectiveTo >= now))
            .Join(
                dbContext.WarrantyPlans.AsNoTrackingIfEf().Where(plan => plan.IsActive &&
                    plan.EffectiveFrom <= now && (plan.EffectiveTo == null || plan.EffectiveTo >= now)),
                mapping => mapping.WarrantyPlanId,
                plan => plan.Id,
                (mapping, _) => mapping.ProductVariantId)
            .Distinct()
            .ToArrayAsyncSafe(cancellationToken))
            .ToHashSet();

        var seenFingerprintKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in parsedRows)
        {
            if (row.Identifier is null)
            {
                continue;
            }

            if (!variantsBySku.ContainsKey(row.Sku))
            {
                row.Errors.Add("SKU was not found.");
            }
            else if (!effectivePlanVariantIds.Contains(variantsBySku[row.Sku].Id))
            {
                row.Errors.Add("SKU does not have an effective warranty plan.");
            }

            var key = $"{row.Identifier.IdentifierType}|{row.Fingerprint}";
            if (!seenFingerprintKeys.Add(key))
            {
                row.Errors.Add("Identifier is duplicated in this import.");
            }
        }

        var lookupKeyVersions = options.LookupKeyVersions.ToArray();
        var fingerprints = parsedRows.Where(row => row.Fingerprints.Count > 0)
            .SelectMany(row => row.Fingerprints.Values)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existingFingerprintKeys = (await dbContext.SerializedProductUnits
            .AsNoTrackingIfEf()
            .Where(unit => lookupKeyVersions.Contains(unit.IdentifierKeyVersion) && fingerprints.Contains(unit.IdentifierFingerprint))
            .Select(unit => new { unit.IdentifierType, unit.IdentifierKeyVersion, unit.IdentifierFingerprint })
            .ToArrayAsyncSafe(cancellationToken))
            .Select(unit => $"{unit.IdentifierType}|{unit.IdentifierKeyVersion}|{unit.IdentifierFingerprint}")
            .ToHashSet(StringComparer.Ordinal);
        foreach (var row in parsedRows.Where(row => row.Identifier is not null))
        {
            if (row.Fingerprints.Any(pair => existingFingerprintKeys.Contains($"{row.Identifier!.IdentifierType}|{pair.Key}|{pair.Value}")))
            {
                row.Errors.Add("Identifier is already provisioned.");
            }
        }

        var rowResults = parsedRows.Select(row => new AdminWarrantyImportRowResultDto(
            row.RowNumber,
            row.Sku,
            row.Identifier?.IdentifierType ?? row.RequestedIdentifierType,
            row.Errors.Count == 0,
            row.Errors.ToArray())).ToArray();
        var failedRows = rowResults.Count(row => !row.IsValid);
        if (failedRows > 0)
        {
            WarrantyMetrics.RecordImport(rowResults.Length, committed: false, valid: false);
            return Result<AdminWarrantyImportResultDto>.Success(new AdminWarrantyImportResultDto(
                request.DryRun,
                false,
                null,
                rowResults.Length,
                0,
                failedRows,
                rowResults));
        }

        if (request.DryRun)
        {
            WarrantyMetrics.RecordImport(rowResults.Length, committed: false, valid: true);
            return Result<AdminWarrantyImportResultDto>.Success(new AdminWarrantyImportResultDto(
                true,
                true,
                null,
                rowResults.Length,
                0,
                0,
                rowResults));
        }

        var checksum = ComputeImportChecksum(parsedRows);
        var existingBatch = await dbContext.WarrantyImportBatches
            .AsNoTrackingIfEf()
            .Where(batch => batch.ContentChecksum == checksum)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (existingBatch is not null)
        {
            return Result<AdminWarrantyImportResultDto>.Success(new AdminWarrantyImportResultDto(
                false,
                true,
                existingBatch.Id,
                existingBatch.TotalRows,
                existingBatch.ImportedRows,
                existingBatch.FailedRows,
                rowResults));
        }

        var batch = new WarrantyImportBatch(Guid.NewGuid(), checksum, NormalizeActor(actorId), parsedRows.Length, now);
        dbContext.Add(batch);
        foreach (var row in parsedRows)
        {
            var unit = new SerializedProductUnit(
                Guid.NewGuid(),
                variantsBySku[row.Sku].Id,
                row.Identifier!.IdentifierType,
                options.IdentifierKeyVersion,
                row.Fingerprint!,
                row.Identifier.MaskedValue,
                batch.Id,
                now);
            dbContext.Add(unit);
            AddAudit(null, unit.Id, WarrantyAuditAction.UnitImported, "Admin", actorId, null, now);
        }

        batch.Complete(parsedRows.Length, 0, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        WarrantyMetrics.RecordImport(parsedRows.Length, committed: true, valid: true);
        return Result<AdminWarrantyImportResultDto>.Success(new AdminWarrantyImportResultDto(
            false,
            true,
            batch.Id,
            parsedRows.Length,
            parsedRows.Length,
            0,
            rowResults));
    }

    public async Task<Result<PagedResult<AdminWarrantyUnitDto>>> GetUnitsAsync(
        AdminWarrantyUnitListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<PagedResult<AdminWarrantyUnitDto>>.NotFound("Warranty administration is unavailable.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSearch = NormalizeOptional(request.Search)?.ToUpperInvariant();
        var query = dbContext.SerializedProductUnits.AsNoTrackingIfEf();
        if (request.Status.HasValue)
        {
            query = query.Where(unit => unit.Status == request.Status.Value);
        }

        if (normalizedSearch is not null)
        {
            query = query.Where(unit => unit.MaskedIdentifier.ToUpper().Contains(normalizedSearch) ||
                dbContext.ProductVariants.Any(variant => variant.Id == unit.ProductVariantId && variant.Sku.ToUpper().Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var units = await query
            .OrderByDescending(unit => unit.CreatedAt)
            .ThenBy(unit => unit.Id)
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .Select(unit => new AdminWarrantyUnitDto(
                unit.Id,
                unit.ProductVariantId,
                dbContext.ProductVariants.Where(variant => variant.Id == unit.ProductVariantId).Select(variant => variant.Sku).FirstOrDefault() ?? string.Empty,
                dbContext.ProductVariants.Where(variant => variant.Id == unit.ProductVariantId).Select(variant => variant.Name).FirstOrDefault() ?? string.Empty,
                unit.IdentifierType,
                unit.MaskedIdentifier,
                unit.Status,
                unit.OrderItemId,
                unit.OrderItemId == null ? null : dbContext.OrderItems.Where(item => item.Id == unit.OrderItemId).Join(dbContext.Orders, item => item.OrderId, order => order.Id, (item, order) => order.OrderCode).FirstOrDefault(),
                unit.AssignedAt,
                unit.ImportBatchId,
                unit.CreatedAt))
            .ToArrayAsyncSafe(cancellationToken);

        return Result<PagedResult<AdminWarrantyUnitDto>>.Success(new PagedResult<AdminWarrantyUnitDto>(
            units,
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            totalCount));
    }

    public async Task<Result<AdminWarrantyEntitlementDto>> AssignUnitAsync(
        Guid unitId,
        AssignWarrantyUnitRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty administration is unavailable.");
        }

        if (request.OrderItemId == Guid.Empty)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation(["Order item is required."]);
        }

        Result<AdminWarrantyEntitlementDto>? outcome = null;
        Guid? entitlementId = null;
        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var unit = await dbContext.FindSerializedProductUnitByIdForUpdateAsync(unitId, transactionCancellationToken);
                if (unit is null)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.NotFound("Warranty unit was not found.");
                    return;
                }

                var orderItem = await dbContext.OrderItems
                    .Where(candidate => candidate.Id == request.OrderItemId)
                    .FirstOrDefaultAsyncSafe(transactionCancellationToken);
                if (orderItem is null)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.NotFound("Order item was not found.");
                    return;
                }

                // The order row is a shared lock scope for unit-count checks,
                // so concurrent packing requests cannot over-assign a line.
                var order = await dbContext.FindOrderForUpdateAsync(orderItem.OrderId, transactionCancellationToken);
                if (order is null)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.NotFound("Order was not found.");
                    return;
                }

                if (unit.ProductVariantId != orderItem.ProductVariantId || order.CustomerId is null ||
                    order.Status is OrderStatus.Cancelled or OrderStatus.Returned)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.Validation(["This unit cannot be assigned to the selected order item."]);
                    return;
                }

                var assignedCount = await dbContext.SerializedProductUnits
                    .Where(candidate => candidate.OrderItemId == orderItem.Id)
                    .CountAsyncSafe(transactionCancellationToken);
                if (assignedCount >= orderItem.Quantity)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.Conflict("All physical units for this order item are already assigned.");
                    return;
                }

                var now = timeProvider.GetUtcNow();
                var mapping = await dbContext.ProductVariantWarrantyPlans
                    .AsNoTrackingIfEf()
                    .Where(candidate => candidate.ProductVariantId == unit.ProductVariantId &&
                        candidate.EffectiveFrom <= now &&
                        (candidate.EffectiveTo == null || candidate.EffectiveTo >= now))
                    .OrderByDescending(candidate => candidate.EffectiveFrom)
                    .ThenByDescending(candidate => candidate.Id)
                    .FirstOrDefaultAsyncSafe(transactionCancellationToken);
                if (mapping is null)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.Validation(["No effective warranty plan is assigned to this product variant."]);
                    return;
                }

                var plan = await dbContext.WarrantyPlans
                    .Where(candidate => candidate.Id == mapping.WarrantyPlanId)
                    .FirstOrDefaultAsyncSafe(transactionCancellationToken);
                if (plan is null)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.Validation(["The assigned warranty plan was not found."]);
                    return;
                }

                var coverages = await GetPlanCoveragesAsync(plan.Id, transactionCancellationToken);
                if (!plan.IsEffectiveAt(now) || coverages.Length == 0)
                {
                    outcome = Result<AdminWarrantyEntitlementDto>.Validation(["The assigned warranty plan is not active or has no coverage components."]);
                    return;
                }

                unit.AssignToOrderItem(orderItem.Id, now);
                var entitlement = new WarrantyEntitlement(
                    Guid.NewGuid(), unit.Id, plan.Id, order.Id, orderItem.Id, order.CustomerId, now);
                dbContext.Update(unit);
                dbContext.Add(entitlement);
                AddAudit(entitlement.Id, unit.Id, WarrantyAuditAction.UnitAssigned, "Admin", actorId, null, now);
                await dbContext.SaveChangesAsync(transactionCancellationToken);
                entitlementId = entitlement.Id;
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation([exception.Message]);
        }

        if (outcome is not null)
        {
            return outcome;
        }

        return entitlementId.HasValue
            ? await GetEntitlementAsync(entitlementId.Value, cancellationToken)
            : Result<AdminWarrantyEntitlementDto>.Validation(["Warranty unit assignment could not be completed."]);
    }

    public async Task<Result<PagedResult<AdminWarrantyEntitlementDto>>> GetEntitlementsAsync(
        AdminWarrantyEntitlementListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<PagedResult<AdminWarrantyEntitlementDto>>.NotFound("Warranty administration is unavailable.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSearch = NormalizeOptional(request.Search)?.ToUpperInvariant();
        var query = dbContext.WarrantyEntitlements.AsNoTrackingIfEf();
        if (request.Status.HasValue)
        {
            query = query.Where(entitlement => entitlement.Status == request.Status.Value);
        }

        if (normalizedSearch is not null)
        {
            query = query.Where(entitlement =>
                dbContext.Orders.Any(order => order.Id == entitlement.OrderId && order.OrderCode.ToUpper().Contains(normalizedSearch)) ||
                dbContext.SerializedProductUnits.Any(unit => unit.Id == entitlement.SerializedProductUnitId && unit.MaskedIdentifier.ToUpper().Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var rows = await query
            .OrderByDescending(entitlement => entitlement.CreatedAt)
            .ThenBy(entitlement => entitlement.Id)
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .Select(entitlement => new
            {
                Entitlement = entitlement,
                Unit = dbContext.SerializedProductUnits.Where(unit => unit.Id == entitlement.SerializedProductUnitId).FirstOrDefault(),
                PlanName = dbContext.WarrantyPlans.Where(plan => plan.Id == entitlement.WarrantyPlanId).Select(plan => plan.Name).FirstOrDefault() ?? string.Empty,
                OrderCode = dbContext.Orders.Where(order => order.Id == entitlement.OrderId).Select(order => order.OrderCode).FirstOrDefault() ?? string.Empty,
                ProductName = dbContext.OrderItems.Where(item => item.Id == entitlement.OrderItemId).Select(item => item.ProductNameSnapshot).FirstOrDefault() ?? string.Empty
            })
            .ToArrayAsyncSafe(cancellationToken);

        var pageItems = rows.Where(row => row.Unit is not null).Select(row => new AdminWarrantyEntitlementDto(
            row.Entitlement.Id,
            row.Entitlement.SerializedProductUnitId,
            row.Unit!.MaskedIdentifier,
            row.Unit.IdentifierType,
            row.Entitlement.WarrantyPlanId,
            row.PlanName,
            row.Entitlement.OrderId,
            row.OrderCode,
            row.Entitlement.CustomerId,
            row.ProductName,
            row.Entitlement.Status,
            row.Entitlement.PurchasedAt,
            row.Entitlement.EligibleAt,
            row.Entitlement.ActivationDeadline,
            row.Entitlement.ActivatedAt,
            row.Entitlement.ActivationSource,
            row.Entitlement.ReplacementSerializedProductUnitId,
            [],
            [])).ToArray();

        return Result<PagedResult<AdminWarrantyEntitlementDto>>.Success(new PagedResult<AdminWarrantyEntitlementDto>(
            pageItems,
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            totalCount));
    }

    public async Task<Result<AdminWarrantyEntitlementDto>> GetEntitlementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty administration is unavailable.");
        }

        var entitlement = await dbContext.WarrantyEntitlements
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        return entitlement is null
            ? Result<AdminWarrantyEntitlementDto>.NotFound("Warranty entitlement was not found.")
            : Result<AdminWarrantyEntitlementDto>.Success(await ToAdminEntitlementDtoAsync(entitlement, cancellationToken));
    }

    public Task<Result<AdminWarrantyEntitlementDto>> ActivateAsync(Guid id, string actorId, CancellationToken cancellationToken = default) =>
        ActivateCoreAsync(id, WarrantyActivationSource.Admin, actorId, cancellationToken);

    public async Task<Result<AdminWarrantyEntitlementDto>> VoidAsync(
        Guid id,
        AdminWarrantyReasonRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty administration is unavailable.");
        }

        var validation = await reasonValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var entitlement = await dbContext.WarrantyEntitlements.Where(candidate => candidate.Id == id).FirstOrDefaultAsyncSafe(cancellationToken);
        if (entitlement is null)
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty entitlement was not found.");
        }

        var unit = await dbContext.SerializedProductUnits.Where(candidate => candidate.Id == entitlement.SerializedProductUnitId).FirstOrDefaultAsyncSafe(cancellationToken);
        if (unit is null)
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty unit was not found.");
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            entitlement.Void(now);
            unit.Void(now);
            dbContext.Update(entitlement);
            dbContext.Update(unit);
            AddAudit(entitlement.Id, unit.Id, WarrantyAuditAction.Voided, "Admin", actorId, request.Reason, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<AdminWarrantyEntitlementDto>.Success(await ToAdminEntitlementDtoAsync(entitlement, cancellationToken));
        }
        catch (DomainException exception)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminWarrantyEntitlementDto>> ReplaceAsync(
        Guid id,
        ReplaceWarrantyRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty administration is unavailable.");
        }

        var validation = await replaceValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
        }

        var oldEntitlement = await dbContext.WarrantyEntitlements.Where(candidate => candidate.Id == id).FirstOrDefaultAsyncSafe(cancellationToken);
        if (oldEntitlement is null)
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty entitlement was not found.");
        }

        var replacementEntitlement = await dbContext.WarrantyEntitlements
            .Where(candidate => candidate.SerializedProductUnitId == request.ReplacementSerializedProductUnitId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        var oldUnit = await dbContext.SerializedProductUnits.Where(candidate => candidate.Id == oldEntitlement.SerializedProductUnitId).FirstOrDefaultAsyncSafe(cancellationToken);
        var replacementUnit = await dbContext.SerializedProductUnits.Where(candidate => candidate.Id == request.ReplacementSerializedProductUnitId).FirstOrDefaultAsyncSafe(cancellationToken);
        if (replacementEntitlement is null || oldUnit is null || replacementUnit is null ||
            replacementEntitlement.Status != WarrantyEntitlementStatus.PendingActivation ||
            replacementEntitlement.OrderId != oldEntitlement.OrderId ||
            oldEntitlement.Status != WarrantyEntitlementStatus.Active ||
            oldEntitlement.PurchasedAt is null || oldEntitlement.EligibleAt is null || oldEntitlement.ActivationDeadline is null ||
            oldEntitlement.ActivatedAt is null || oldEntitlement.AcceptedTermsVersion is null)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation(["Replacement must be a pending unit assigned to the same order as an active warranty."]);
        }

        var oldSnapshots = await dbContext.WarrantyCoverageSnapshots
            .AsNoTrackingIfEf()
            .Where(snapshot => snapshot.WarrantyEntitlementId == oldEntitlement.Id)
            .OrderBy(snapshot => snapshot.SortOrder)
            .ThenBy(snapshot => snapshot.Id)
            .ToArrayAsyncSafe(cancellationToken);
        if (oldSnapshots.Length == 0)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation(["The existing warranty has no coverage snapshots to carry forward."]);
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            var snapshots = oldSnapshots.Select(snapshot => new WarrantyCoverageSnapshot(
                Guid.NewGuid(), replacementEntitlement.Id, snapshot.ComponentCode, snapshot.DisplayName,
                snapshot.DurationMonths, snapshot.StartsAt, snapshot.EndsAt, snapshot.SortOrder)).ToArray();
            replacementEntitlement.Activate(
                oldEntitlement.PurchasedAt.Value,
                oldEntitlement.EligibleAt.Value,
                oldEntitlement.ActivationDeadline.Value,
                oldEntitlement.ActivatedAt.Value,
                WarrantyActivationSource.Admin,
                oldEntitlement.AcceptedTermsVersion,
                snapshots);
            oldEntitlement.MarkReplaced(replacementUnit.Id, now);
            oldUnit.MarkReplaced(now);
            replacementUnit.Activate(now);
            dbContext.Update(oldEntitlement);
            dbContext.Update(oldUnit);
            dbContext.Update(replacementEntitlement);
            dbContext.Update(replacementUnit);
            foreach (var snapshot in snapshots)
            {
                dbContext.Add(snapshot);
            }

            AddAudit(oldEntitlement.Id, oldUnit.Id, WarrantyAuditAction.Replaced, "Admin", actorId, request.Reason, now);
            AddAudit(replacementEntitlement.Id, replacementUnit.Id, WarrantyAuditAction.Activated, "Admin", actorId, "Replacement coverage carried forward.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<AdminWarrantyEntitlementDto>.Success(await ToAdminEntitlementDtoAsync(oldEntitlement, cancellationToken));
        }
        catch (DomainException exception)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation([exception.Message]);
        }
    }

    private async Task<Result<AdminWarrantyEntitlementDto>> ActivateCoreAsync(
        Guid id,
        WarrantyActivationSource source,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (!IsAdminEnabled())
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty administration is unavailable.");
        }

        var entitlement = await dbContext.WarrantyEntitlements.Where(candidate => candidate.Id == id).FirstOrDefaultAsyncSafe(cancellationToken);
        if (entitlement is null)
        {
            return Result<AdminWarrantyEntitlementDto>.NotFound("Warranty entitlement was not found.");
        }

        if (entitlement.Status == WarrantyEntitlementStatus.Active)
        {
            WarrantyMetrics.RecordActivation("idempotent", "admin");
            return Result<AdminWarrantyEntitlementDto>.Success(await ToAdminEntitlementDtoAsync(entitlement, cancellationToken));
        }

        var unit = await dbContext.SerializedProductUnits.Where(candidate => candidate.Id == entitlement.SerializedProductUnitId).FirstOrDefaultAsyncSafe(cancellationToken);
        var order = await dbContext.Orders.Where(candidate => candidate.Id == entitlement.OrderId).FirstOrDefaultAsyncSafe(cancellationToken);
        var plan = await dbContext.WarrantyPlans.Where(candidate => candidate.Id == entitlement.WarrantyPlanId).FirstOrDefaultAsyncSafe(cancellationToken);
        if (unit is null || order is null || plan is null)
        {
            return Result<AdminWarrantyEntitlementDto>.Validation(["Warranty activation data is incomplete."]);
        }

        var coverages = await GetPlanCoveragesAsync(plan.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        try
        {
            var eligibility = WarrantyActivationRules.GetEligibility(order, plan, now);
            var snapshots = WarrantyActivationRules.CreateCoverageSnapshots(entitlement, coverages, eligibility.PurchasedAt);
            entitlement.Activate(eligibility.PurchasedAt, eligibility.EligibleAt, eligibility.ActivationDeadline, now, source, plan.TermsVersion, snapshots);
            unit.Activate(now);
            dbContext.Update(entitlement);
            dbContext.Update(unit);
            foreach (var snapshot in snapshots)
            {
                dbContext.Add(snapshot);
            }

            AddAudit(entitlement.Id, unit.Id, WarrantyAuditAction.Activated, "Admin", actorId, null, now);
            QueueActivationEmail(order, unit, plan, coverages);
            await dbContext.SaveChangesAsync(cancellationToken);
            WarrantyMetrics.RecordActivation("activated", "admin");
            return Result<AdminWarrantyEntitlementDto>.Success(await ToAdminEntitlementDtoAsync(entitlement, cancellationToken));
        }
        catch (DomainException exception)
        {
            WarrantyMetrics.RecordActivation("rejected", "admin");
            return Result<AdminWarrantyEntitlementDto>.Validation([exception.Message]);
        }
    }

    private async Task<AdminWarrantyEntitlementDto> ToAdminEntitlementDtoAsync(WarrantyEntitlement entitlement, CancellationToken cancellationToken)
    {
        var unit = await dbContext.SerializedProductUnits.AsNoTrackingIfEf().Where(candidate => candidate.Id == entitlement.SerializedProductUnitId).FirstOrDefaultAsyncSafe(cancellationToken)
            ?? throw new InvalidOperationException("Warranty entitlement references a missing product unit.");
        var plan = await dbContext.WarrantyPlans.AsNoTrackingIfEf().Where(candidate => candidate.Id == entitlement.WarrantyPlanId).FirstOrDefaultAsyncSafe(cancellationToken)
            ?? throw new InvalidOperationException("Warranty entitlement references a missing plan.");
        var order = await dbContext.Orders.AsNoTrackingIfEf().Where(candidate => candidate.Id == entitlement.OrderId).FirstOrDefaultAsyncSafe(cancellationToken)
            ?? throw new InvalidOperationException("Warranty entitlement references a missing order.");
        var item = await dbContext.OrderItems.AsNoTrackingIfEf().Where(candidate => candidate.Id == entitlement.OrderItemId).FirstOrDefaultAsyncSafe(cancellationToken)
            ?? throw new InvalidOperationException("Warranty entitlement references a missing order item.");
        var coverages = await dbContext.WarrantyCoverageSnapshots.AsNoTrackingIfEf().Where(snapshot => snapshot.WarrantyEntitlementId == entitlement.Id)
            .OrderBy(snapshot => snapshot.SortOrder).ThenBy(snapshot => snapshot.Id).ToArrayAsyncSafe(cancellationToken);
        var auditEvents = await dbContext.WarrantyAuditEvents.AsNoTrackingIfEf().Where(@event => @event.WarrantyEntitlementId == entitlement.Id)
            .OrderByDescending(@event => @event.OccurredAt).ThenByDescending(@event => @event.Id).Take(100).ToArrayAsyncSafe(cancellationToken);
        return new AdminWarrantyEntitlementDto(
            entitlement.Id, unit.Id, unit.MaskedIdentifier, unit.IdentifierType, plan.Id, plan.Name, order.Id, order.OrderCode,
            entitlement.CustomerId, item.ProductNameSnapshot, entitlement.Status, entitlement.PurchasedAt, entitlement.EligibleAt,
            entitlement.ActivationDeadline, entitlement.ActivatedAt, entitlement.ActivationSource, entitlement.ReplacementSerializedProductUnitId,
            coverages.Select(ToCoverageDto).ToArray(),
            auditEvents.Select(@event => new WarrantyAuditEventDto(@event.Id, @event.Action, @event.ActorType, @event.ActorId, @event.Reason, @event.OccurredAt)).ToArray());
    }

    private Task<WarrantyPlanCoverage[]> GetPlanCoveragesAsync(Guid planId, CancellationToken cancellationToken) => dbContext.WarrantyPlanCoverages
        .Where(coverage => coverage.WarrantyPlanId == planId)
        .OrderBy(coverage => coverage.SortOrder).ThenBy(coverage => coverage.ComponentCode)
        .ToArrayAsyncSafe(cancellationToken);

    private void AddAudit(Guid? entitlementId, Guid? unitId, WarrantyAuditAction action, string actorType, string actorId, string? reason, DateTimeOffset now) =>
        dbContext.Add(new WarrantyAuditEvent(Guid.NewGuid(), entitlementId, unitId, action, actorType, NormalizeActor(actorId), reason, Guid.NewGuid().ToString("N"), now));

    private void QueueActivationEmail(Order order, SerializedProductUnit unit, WarrantyPlan plan, IReadOnlyCollection<WarrantyPlanCoverage> coverages)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return;
        }

        var coverageText = string.Join(", ", coverages.OrderBy(coverage => coverage.SortOrder).Select(coverage => $"{coverage.DisplayName}: {coverage.DurationMonths} months"));
        customerEmailOutbox.Enqueue(new CustomerEmailMessage(
            order.CustomerEmail,
            "Your product warranty is active",
            $"Your warranty for {unit.MaskedIdentifier} is active under plan {plan.Name}. Coverage: {coverageText}."));
    }

    private ParsedImportRow ParseImportRow(WarrantyUnitImportRow row)
    {
        var parsed = new ParsedImportRow(row.RowNumber, row.Sku.Trim().ToUpperInvariant(), row.IdentifierType);
        if (row.RowNumber <= 0)
        {
            parsed.Errors.Add("Row number is invalid.");
        }

        if (string.IsNullOrWhiteSpace(parsed.Sku) || parsed.Sku.Length > 100)
        {
            parsed.Errors.Add("SKU is invalid.");
        }

        if (row.HasInvalidIdentifierType)
        {
            parsed.Errors.Add("Identifier type must be Serial or IMEI.");
            return parsed;
        }

        try
        {
            parsed.Identifier = identifierProtector.Normalize(row.IdentifierType, row.Identifier);
            foreach (var keyVersion in options.LookupKeyVersions)
            {
                parsed.Fingerprints[keyVersion] = identifierProtector.CreateFingerprint(
                    parsed.Identifier.IdentifierType,
                    parsed.Identifier.NormalizedValue,
                    keyVersion);
            }

            parsed.Fingerprint = parsed.Fingerprints[options.IdentifierKeyVersion];
        }
        catch (DomainException exception)
        {
            parsed.Errors.Add(exception.Message);
        }

        return parsed;
    }

    private static string ComputeImportChecksum(IEnumerable<ParsedImportRow> rows)
    {
        var material = string.Join("\n", rows.OrderBy(row => row.RowNumber).Select(row => $"{row.Sku}|{row.Identifier!.IdentifierType}|{row.Fingerprint}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static AdminWarrantyPlanDto ToPlanDto(WarrantyPlan plan, IEnumerable<WarrantyPlanCoverage> coverages) => new(
        plan.Id, plan.Code, plan.Name, plan.ActivationWindowDays, plan.TermsVersion, plan.EffectiveFrom, plan.EffectiveTo,
        plan.IsActive, plan.CreatedAt, plan.UpdatedAt, coverages.Select(ToCoverageDto).ToArray());

    private static WarrantyCoverageDto ToCoverageDto(WarrantyPlanCoverage coverage) => new(
        coverage.ComponentCode, coverage.DisplayName, coverage.DurationMonths, null, null, coverage.SortOrder);

    private static WarrantyCoverageDto ToCoverageDto(WarrantyCoverageSnapshot coverage) => new(
        coverage.ComponentCode, coverage.DisplayName, coverage.DurationMonths, coverage.StartsAt, coverage.EndsAt, coverage.SortOrder);

    private bool IsAdminEnabled() => options.Enabled && options.AdminEnabled;

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeActor(string actorId) => string.IsNullOrWhiteSpace(actorId) ? "admin" : actorId.Trim();

    private sealed class ParsedImportRow(int rowNumber, string sku, WarrantyIdentifierType? requestedIdentifierType)
    {
        public int RowNumber { get; } = rowNumber;
        public string Sku { get; } = sku;
        public WarrantyIdentifierType? RequestedIdentifierType { get; } = requestedIdentifierType;
        public WarrantyIdentifier? Identifier { get; set; }
        public string? Fingerprint { get; set; }
        public Dictionary<int, string> Fingerprints { get; } = [];
        public List<string> Errors { get; } = [];
    }
}
