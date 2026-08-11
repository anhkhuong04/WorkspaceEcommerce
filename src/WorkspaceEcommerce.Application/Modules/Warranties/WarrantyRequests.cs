using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

public sealed class WarrantyPlanCoverageInput
{
    public string ComponentCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int DurationMonths { get; init; }

    public int SortOrder { get; init; }
}

public sealed class CreateWarrantyPlanRequest
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int ActivationWindowDays { get; init; } = 60;

    public string TermsVersion { get; init; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; init; }

    public DateTimeOffset? EffectiveTo { get; init; }

    public WarrantyPlanCoverageInput[] Coverages { get; init; } = [];
}

public sealed record AdminWarrantyPlanListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }

    public bool? IsActive { get; init; }

    public int NormalizedPageNumber => PageNumber < 1 ? 1 : PageNumber;
    public int NormalizedPageSize => PageSize switch { < 1 => 20, > 100 => 100, _ => PageSize };
    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;
}

public sealed class AssignWarrantyPlanRequest
{
    public Guid WarrantyPlanId { get; init; }

    public DateTimeOffset EffectiveFrom { get; init; }

    public DateTimeOffset? EffectiveTo { get; init; }
}

public sealed class WarrantyUnitImportRow
{
    public int RowNumber { get; init; }

    public string Sku { get; init; } = string.Empty;

    public WarrantyIdentifierType? IdentifierType { get; init; }

    /// <summary>Set by the CSV boundary when a non-empty type column is invalid.</summary>
    public bool HasInvalidIdentifierType { get; init; }

    public string Identifier { get; init; } = string.Empty;
}

public sealed class ImportWarrantyUnitsRequest
{
    public bool DryRun { get; init; }

    public WarrantyUnitImportRow[] Rows { get; init; } = [];
}

public sealed record AdminWarrantyUnitListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }

    public SerializedProductUnitStatus? Status { get; init; }

    public int NormalizedPageNumber => PageNumber < 1 ? 1 : PageNumber;
    public int NormalizedPageSize => PageSize switch { < 1 => 20, > 100 => 100, _ => PageSize };
    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;
}

public sealed class AssignWarrantyUnitRequest
{
    public Guid OrderItemId { get; init; }
}

public sealed class WarrantyLookupRequest
{
    public WarrantyIdentifierType? IdentifierType { get; init; }

    public string Identifier { get; init; } = string.Empty;
}

public sealed class ActivateWarrantyRequest
{
    public WarrantyIdentifierType? IdentifierType { get; init; }

    public string Identifier { get; init; } = string.Empty;
}

public sealed record CustomerWarrantyListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int NormalizedPageNumber => PageNumber < 1 ? 1 : PageNumber;
    public int NormalizedPageSize => PageSize switch { < 1 => 20, > 100 => 100, _ => PageSize };
    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;
}

public sealed record AdminWarrantyEntitlementListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }

    public WarrantyEntitlementStatus? Status { get; init; }

    public int NormalizedPageNumber => PageNumber < 1 ? 1 : PageNumber;
    public int NormalizedPageSize => PageSize switch { < 1 => 20, > 100 => 100, _ => PageSize };
    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;
}

public class AdminWarrantyReasonRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReplaceWarrantyRequest : AdminWarrantyReasonRequest
{
    public Guid ReplacementSerializedProductUnitId { get; init; }
}
