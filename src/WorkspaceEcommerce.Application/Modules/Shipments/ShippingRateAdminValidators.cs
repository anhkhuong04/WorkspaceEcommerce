using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed class SaveShippingRatePlanRequestValidator : AbstractValidator<SaveShippingRatePlanRequest>
{
    public SaveShippingRatePlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.CatalogVersion).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OriginProvinceCode).NotEmpty().Matches("^[0-9]{2}$");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ExpectedRevision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Zones).NotNull().Must(zones => zones is not null && zones.Count <= 100)
            .WithMessage("Provide at most 100 rate zones.");
        RuleForEach(x => x.Zones).NotNull().SetValidator(new ShippingRateZoneRequestValidator());
    }
}

public sealed class ShippingRateZoneRequestValidator : AbstractValidator<ShippingRateZoneRequest>
{
    public ShippingRateZoneRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.BaseWeightGrams).GreaterThan(0);
        RuleFor(x => x.StepWeightGrams).GreaterThan(0);
        RuleFor(x => x.MaximumWeightGrams).Must((zone, maximum) => !maximum.HasValue || maximum.Value >= zone.BaseWeightGrams);
        RuleFor(x => x.BaseFee).InclusiveBetween(0m, 999_999_999_999_999_999m).Must(value => decimal.Truncate(value) == value);
        RuleFor(x => x.StepFee).InclusiveBetween(0m, 999_999_999_999_999_999m).Must(value => decimal.Truncate(value) == value);
        RuleFor(x => x.ProvinceCodes).NotNull().Must(codes => codes is not null && codes.Count is > 0 and <= 100);
        RuleForEach(x => x.ProvinceCodes).NotEmpty().Matches("^[0-9]{2}$");
    }
}

public sealed class ShippingRateActionRequestValidator : AbstractValidator<ShippingRateActionRequest>
{
    public ShippingRateActionRequestValidator()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class PublishShippingRatePlanRequestValidator : AbstractValidator<PublishShippingRatePlanRequest>
{
    public PublishShippingRatePlanRequestValidator()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ExpectedActivePlanId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.ExpectedActiveRevision).Must((request, revision) => request.ExpectedActivePlanId.HasValue
            ? revision is > 0 : !revision.HasValue);
    }
}

public sealed class CloneShippingRatePlanRequestValidator : AbstractValidator<CloneShippingRatePlanRequest>
{
    public CloneShippingRatePlanRequestValidator()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ShippingRatePreviewRequestValidator : AbstractValidator<ShippingRatePreviewRequest>
{
    public ShippingRatePreviewRequestValidator()
    {
        RuleFor(x => x.ProvinceCode).NotEmpty().Matches("^[0-9]{2}$");
        RuleFor(x => x.WardCode).NotEmpty().Matches("^[0-9]{5}$");
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
        RuleFor(x => x.Items).NotNull().Must(items => items is not null && items.Count is > 0 and <= 1000);
        RuleForEach(x => x.Items).NotNull();
    }
}
