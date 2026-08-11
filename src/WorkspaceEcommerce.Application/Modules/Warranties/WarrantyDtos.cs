using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

public sealed record WarrantyCoverageDto(
    string ComponentCode,
    string DisplayName,
    int DurationMonths,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int SortOrder);

public sealed record AdminWarrantyPlanDto(
    Guid Id,
    string Code,
    string Name,
    int ActivationWindowDays,
    string TermsVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<WarrantyCoverageDto> Coverages);

public sealed record AdminWarrantyUnitDto(
    Guid Id,
    Guid ProductVariantId,
    string Sku,
    string VariantName,
    WarrantyIdentifierType IdentifierType,
    string MaskedIdentifier,
    SerializedProductUnitStatus Status,
    Guid? OrderItemId,
    string? OrderCode,
    DateTimeOffset? AssignedAt,
    Guid ImportBatchId,
    DateTimeOffset CreatedAt);

public sealed record AdminWarrantyImportRowResultDto(
    int RowNumber,
    string Sku,
    WarrantyIdentifierType? IdentifierType,
    bool IsValid,
    IReadOnlyCollection<string> Errors);

public sealed record AdminWarrantyImportResultDto(
    bool IsDryRun,
    bool IsValid,
    Guid? ImportBatchId,
    int TotalRows,
    int ImportedRows,
    int FailedRows,
    IReadOnlyCollection<AdminWarrantyImportRowResultDto> Rows);

public sealed record WarrantyAuditEventDto(
    Guid Id,
    WarrantyAuditAction Action,
    string ActorType,
    string ActorId,
    string? Reason,
    DateTimeOffset OccurredAt);

public sealed record AdminWarrantyEntitlementDto(
    Guid Id,
    Guid SerializedProductUnitId,
    string MaskedIdentifier,
    WarrantyIdentifierType IdentifierType,
    Guid WarrantyPlanId,
    string WarrantyPlanName,
    Guid OrderId,
    string OrderCode,
    Guid? CustomerId,
    string ProductName,
    WarrantyEntitlementStatus Status,
    DateTimeOffset? PurchasedAt,
    DateTimeOffset? EligibleAt,
    DateTimeOffset? ActivationDeadline,
    DateTimeOffset? ActivatedAt,
    WarrantyActivationSource? ActivationSource,
    Guid? ReplacementSerializedProductUnitId,
    IReadOnlyCollection<WarrantyCoverageDto> Coverages,
    IReadOnlyCollection<WarrantyAuditEventDto> AuditEvents);

public sealed record CustomerWarrantyListItemDto(
    Guid Id,
    string ProductName,
    string MaskedIdentifier,
    WarrantyIdentifierType IdentifierType,
    WarrantyEntitlementStatus Status,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? LatestCoverageEndsAt);

public sealed record CustomerWarrantyDto(
    Guid Id,
    string ProductName,
    string MaskedIdentifier,
    WarrantyIdentifierType IdentifierType,
    string WarrantyPlanName,
    WarrantyEntitlementStatus Status,
    DateTimeOffset? PurchasedAt,
    DateTimeOffset? ActivationDeadline,
    DateTimeOffset? ActivatedAt,
    IReadOnlyCollection<WarrantyCoverageDto> Coverages);

public sealed record PublicWarrantyLookupResponse(
    bool Found,
    string? ProductName,
    string? MaskedIdentifier,
    WarrantyIdentifierType? IdentifierType,
    WarrantyEntitlementStatus? Status,
    DateTimeOffset? ActivatedAt,
    IReadOnlyCollection<WarrantyCoverageDto> Coverages);
