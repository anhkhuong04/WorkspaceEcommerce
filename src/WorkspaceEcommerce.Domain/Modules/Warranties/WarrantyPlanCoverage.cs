using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class WarrantyPlanCoverage : Entity
{
    public WarrantyPlanCoverage(
        Guid id,
        Guid warrantyPlanId,
        string componentCode,
        string displayName,
        int durationMonths,
        int sortOrder)
        : base(id)
    {
        if (warrantyPlanId == Guid.Empty)
        {
            throw new DomainException("Warranty plan id is required.");
        }

        if (durationMonths is < 1 or > 240)
        {
            throw new DomainException("Warranty coverage duration must be between 1 and 240 months.");
        }

        if (sortOrder < 0)
        {
            throw new DomainException("Warranty coverage sort order cannot be negative.");
        }

        WarrantyPlanId = warrantyPlanId;
        ComponentCode = WarrantyPlan.NormalizeComponentCode(componentCode);
        DisplayName = Guard.Required(displayName, nameof(DisplayName));
        DurationMonths = durationMonths;
        SortOrder = sortOrder;
    }

    public Guid WarrantyPlanId { get; private set; }

    public string ComponentCode { get; private set; }

    public string DisplayName { get; private set; }

    public int DurationMonths { get; private set; }

    public int SortOrder { get; private set; }
}
