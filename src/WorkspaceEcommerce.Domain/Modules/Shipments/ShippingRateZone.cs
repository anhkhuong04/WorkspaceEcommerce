using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Shipments;

public sealed class ShippingRateZone : Entity
{
    private readonly List<ShippingZoneProvince> _provinces = [];
    private ShippingRateZone() { }

    public ShippingRateZone(Guid id, Guid ratePlanId, string code, string name,
        int baseWeightGrams, decimal baseFee, int stepWeightGrams, decimal stepFee,
        int? maximumWeightGrams, IEnumerable<string> provinceCodes) : base(id)
    {
        if (ratePlanId == Guid.Empty) throw new DomainException("Rate plan id is required.");
        RatePlanId = ratePlanId;
        Code = ShippingRatePlan.Required(code, 50, nameof(Code));
        Name = ShippingRatePlan.Required(name, 120, nameof(Name));
        if (baseWeightGrams <= 0 || stepWeightGrams <= 0 || maximumWeightGrams < baseWeightGrams)
            throw new DomainException("Weight limits and steps must be positive; maximum must cover the base weight.");
        ValidateMoney(baseFee);
        ValidateMoney(stepFee);
        BaseWeightGrams = baseWeightGrams;
        BaseFee = baseFee;
        StepWeightGrams = stepWeightGrams;
        StepFee = stepFee;
        MaximumWeightGrams = maximumWeightGrams;
        var codes = provinceCodes.Select(value => ShippingRatePlan.Required(value, 2, "Province code")).ToArray();
        if (codes.Length == 0 || codes.Length > 100 || codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
            throw new DomainException("A zone must contain distinct province codes (maximum 100).");
        _provinces.AddRange(codes.Select(value => new ShippingZoneProvince(Guid.NewGuid(), id, ratePlanId, value)));
    }

    public Guid RatePlanId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int BaseWeightGrams { get; private set; }
    public decimal BaseFee { get; private set; }
    public int StepWeightGrams { get; private set; }
    public decimal StepFee { get; private set; }
    public int? MaximumWeightGrams { get; private set; }
    public IReadOnlyCollection<ShippingZoneProvince> Provinces => _provinces.AsReadOnly();

    public static void ValidateMoney(decimal amount)
    {
        if (amount < 0 || amount > 999_999_999_999_999_999m || decimal.Truncate(amount) != amount)
            throw new DomainException("Shipping money must be a nonnegative whole VND amount within precision 18.");
    }
}

public sealed class ShippingZoneProvince : Entity
{
    private ShippingZoneProvince() { }
    internal ShippingZoneProvince(Guid id, Guid zoneId, Guid ratePlanId, string provinceCode) : base(id)
    {
        ZoneId = zoneId;
        RatePlanId = ratePlanId;
        ProvinceCode = provinceCode;
    }
    public Guid ZoneId { get; private set; }
    public Guid RatePlanId { get; private set; }
    public string ProvinceCode { get; private set; } = string.Empty;
}
