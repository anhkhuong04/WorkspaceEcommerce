using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class WarrantyPlan : Entity
{
    private readonly List<WarrantyPlanCoverage> _coverages = [];

    public WarrantyPlan(
        Guid id,
        string code,
        string name,
        int activationWindowDays,
        string termsVersion,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        bool isActive = true)
        : base(id)
    {
        Code = NormalizeCode(code);
        Name = Guard.Required(name, nameof(Name));
        ActivationWindowDays = ValidateActivationWindow(activationWindowDays);
        TermsVersion = Guard.Required(termsVersion, nameof(TermsVersion));
        EffectiveFrom = RequireTimestamp(effectiveFrom, nameof(EffectiveFrom));
        EffectiveTo = effectiveTo;
        ValidateEffectiveWindow(EffectiveFrom, EffectiveTo);
        IsActive = isActive;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public int ActivationWindowDays { get; private set; }

    public string TermsVersion { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<WarrantyPlanCoverage> Coverages => _coverages;

    public WarrantyPlanCoverage AddCoverage(
        Guid id,
        string componentCode,
        string displayName,
        int durationMonths,
        int sortOrder)
    {
        if (_coverages.Any(coverage => string.Equals(
                coverage.ComponentCode,
                NormalizeComponentCode(componentCode),
                StringComparison.Ordinal)))
        {
            throw new DomainException("Warranty coverage component must be unique within a plan.");
        }

        var coverage = new WarrantyPlanCoverage(
            id,
            Id,
            NormalizeComponentCode(componentCode),
            displayName,
            durationMonths,
            sortOrder);
        _coverages.Add(coverage);
        Touch();
        return coverage;
    }

    public void Retire(DateTimeOffset retiredAt)
    {
        retiredAt = RequireTimestamp(retiredAt, nameof(retiredAt));
        if (retiredAt < EffectiveFrom)
        {
            throw new DomainException("Warranty plan retirement cannot be before its effective date.");
        }

        EffectiveTo = retiredAt;
        IsActive = false;
        Touch();
    }

    public bool IsEffectiveAt(DateTimeOffset at) =>
        IsActive && at >= EffectiveFrom && (EffectiveTo is null || at <= EffectiveTo.Value);

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string NormalizeCode(string value)
    {
        var code = Guard.Required(value, nameof(Code)).ToUpperInvariant();
        if (code.Length > 50 || !code.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
        {
            throw new DomainException("Warranty plan code must use letters, numbers, underscores, or hyphens and be at most 50 characters.");
        }

        return code;
    }

    internal static string NormalizeComponentCode(string value)
    {
        var code = Guard.Required(value, nameof(value)).ToUpperInvariant();
        if (code.Length > 50 || !code.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
        {
            throw new DomainException("Warranty component code must use letters, numbers, underscores, or hyphens and be at most 50 characters.");
        }

        return code;
    }

    private static int ValidateActivationWindow(int value)
    {
        if (value is < 1 or > 365)
        {
            throw new DomainException("Warranty activation window must be between 1 and 365 days.");
        }

        return value;
    }

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string name)
    {
        if (value == default)
        {
            throw new DomainException($"{name} is required.");
        }

        return value;
    }

    private static void ValidateEffectiveWindow(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
        {
            throw new DomainException("Warranty plan end time must be after its effective date.");
        }
    }
}
