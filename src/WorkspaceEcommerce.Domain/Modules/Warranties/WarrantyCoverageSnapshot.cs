using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class WarrantyCoverageSnapshot : Entity
{
    public WarrantyCoverageSnapshot(
        Guid id,
        Guid warrantyEntitlementId,
        string componentCode,
        string displayName,
        int durationMonths,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int sortOrder)
        : base(id)
    {
        if (warrantyEntitlementId == Guid.Empty || durationMonths < 1 || startsAt == default || endsAt <= startsAt || sortOrder < 0)
        {
            throw new DomainException("Warranty coverage snapshot values are invalid.");
        }

        WarrantyEntitlementId = warrantyEntitlementId;
        ComponentCode = WarrantyPlan.NormalizeComponentCode(componentCode);
        DisplayName = Guard.Required(displayName, nameof(DisplayName));
        DurationMonths = durationMonths;
        StartsAt = startsAt;
        EndsAt = endsAt;
        SortOrder = sortOrder;
    }

    public Guid WarrantyEntitlementId { get; private set; }

    public string ComponentCode { get; private set; }

    public string DisplayName { get; private set; }

    public int DurationMonths { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public int SortOrder { get; private set; }
}
