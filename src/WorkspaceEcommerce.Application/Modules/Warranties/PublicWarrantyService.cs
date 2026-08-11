using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Warranties;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

internal sealed class PublicWarrantyService(
    IAppDbContext dbContext,
    IWarrantyIdentifierProtector identifierProtector,
    WarrantyOptions options,
    IValidator<WarrantyLookupRequest> lookupValidator) : IPublicWarrantyService
{
    private static readonly PublicWarrantyLookupResponse NotFoundResponse = new(false, null, null, null, null, null, []);

    public async Task<Result<PublicWarrantyLookupResponse>> LookupAsync(
        WarrantyLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || !options.PublicLookupEnabled)
        {
            return Result<PublicWarrantyLookupResponse>.NotFound("Warranty lookup is unavailable.");
        }

        var validation = await lookupValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            WarrantyMetrics.RecordLookup(found: false);
            return Result<PublicWarrantyLookupResponse>.Success(NotFoundResponse);
        }

        WarrantyIdentifier identifier;
        IReadOnlyDictionary<int, string> fingerprintsByKeyVersion;
        try
        {
            identifier = identifierProtector.Normalize(request.IdentifierType, request.Identifier);
            fingerprintsByKeyVersion = options.LookupKeyVersions.ToDictionary(
                keyVersion => keyVersion,
                keyVersion => identifierProtector.CreateFingerprint(identifier.IdentifierType, identifier.NormalizedValue, keyVersion));
        }
        catch (DomainException)
        {
            WarrantyMetrics.RecordLookup(found: false);
            return Result<PublicWarrantyLookupResponse>.Success(NotFoundResponse);
        }

        var candidateUnits = await dbContext.SerializedProductUnits
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.IdentifierType == identifier.IdentifierType &&
                fingerprintsByKeyVersion.Keys.Contains(candidate.IdentifierKeyVersion) &&
                fingerprintsByKeyVersion.Values.Contains(candidate.IdentifierFingerprint))
            .ToArrayAsyncSafe(cancellationToken);
        var unit = candidateUnits.FirstOrDefault(candidate =>
            fingerprintsByKeyVersion.TryGetValue(candidate.IdentifierKeyVersion, out var expectedFingerprint) &&
            string.Equals(candidate.IdentifierFingerprint, expectedFingerprint, StringComparison.Ordinal));
        if (unit is null)
        {
            WarrantyMetrics.RecordLookup(found: false);
            return Result<PublicWarrantyLookupResponse>.Success(NotFoundResponse);
        }

        var entitlement = await dbContext.WarrantyEntitlements
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.SerializedProductUnitId == unit.Id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (entitlement is null)
        {
            // Imported but unassigned inventory is deliberately not public.
            WarrantyMetrics.RecordLookup(found: false);
            return Result<PublicWarrantyLookupResponse>.Success(NotFoundResponse);
        }

        var productName = await dbContext.OrderItems
            .AsNoTrackingIfEf()
            .Where(item => item.Id == entitlement.OrderItemId)
            .Select(item => item.ProductNameSnapshot)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        var coverages = await dbContext.WarrantyCoverageSnapshots
            .AsNoTrackingIfEf()
            .Where(snapshot => snapshot.WarrantyEntitlementId == entitlement.Id)
            .OrderBy(snapshot => snapshot.SortOrder)
            .ThenBy(snapshot => snapshot.Id)
            .Select(snapshot => new WarrantyCoverageDto(
                snapshot.ComponentCode,
                snapshot.DisplayName,
                snapshot.DurationMonths,
                snapshot.StartsAt,
                snapshot.EndsAt,
                snapshot.SortOrder))
            .ToArrayAsyncSafe(cancellationToken);

        WarrantyMetrics.RecordLookup(found: true);
        return Result<PublicWarrantyLookupResponse>.Success(new PublicWarrantyLookupResponse(
            true,
            productName,
            unit.MaskedIdentifier,
            unit.IdentifierType,
            entitlement.Status,
            entitlement.ActivatedAt,
            coverages));
    }
}
