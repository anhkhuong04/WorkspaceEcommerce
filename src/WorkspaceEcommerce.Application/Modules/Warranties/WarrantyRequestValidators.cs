using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

public sealed class CreateWarrantyPlanRequestValidator : AbstractValidator<CreateWarrantyPlanRequest>
{
    public CreateWarrantyPlanRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(50)
            .Must(value => value.Trim().All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
            .WithMessage("Code must use letters, numbers, underscores, or hyphens.");
        RuleFor(request => request.Name).NotEmpty().MaximumLength(250);
        RuleFor(request => request.ActivationWindowDays).InclusiveBetween(1, 365);
        RuleFor(request => request.TermsVersion).NotEmpty().MaximumLength(100);
        RuleFor(request => request.EffectiveFrom).NotEqual(default(DateTimeOffset));
        RuleFor(request => request.EffectiveTo).GreaterThan(request => request.EffectiveFrom)
            .When(request => request.EffectiveTo.HasValue);
        RuleFor(request => request.Coverages).NotEmpty().Must(coverages => coverages.Length <= 20);
        RuleForEach(request => request.Coverages).SetValidator(new WarrantyPlanCoverageInputValidator());
        RuleFor(request => request.Coverages)
            .Must(coverages => coverages.Select(coverage => coverage.ComponentCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == coverages.Length)
            .WithMessage("Warranty coverage component codes must be unique.");
    }
}

public sealed class WarrantyPlanCoverageInputValidator : AbstractValidator<WarrantyPlanCoverageInput>
{
    public WarrantyPlanCoverageInputValidator()
    {
        RuleFor(coverage => coverage.ComponentCode).NotEmpty().MaximumLength(50)
            .Must(value => value.Trim().All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
            .WithMessage("Component code must use letters, numbers, underscores, or hyphens.");
        RuleFor(coverage => coverage.DisplayName).NotEmpty().MaximumLength(250);
        RuleFor(coverage => coverage.DurationMonths).InclusiveBetween(1, 240);
        RuleFor(coverage => coverage.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AssignWarrantyPlanRequestValidator : AbstractValidator<AssignWarrantyPlanRequest>
{
    public AssignWarrantyPlanRequestValidator()
    {
        RuleFor(request => request.WarrantyPlanId).NotEmpty();
        RuleFor(request => request.EffectiveFrom).NotEqual(default(DateTimeOffset));
        RuleFor(request => request.EffectiveTo).GreaterThan(request => request.EffectiveFrom)
            .When(request => request.EffectiveTo.HasValue);
    }
}

public sealed class ImportWarrantyUnitsRequestValidator : AbstractValidator<ImportWarrantyUnitsRequest>
{
    public ImportWarrantyUnitsRequestValidator()
    {
        RuleFor(request => request.Rows).NotEmpty().Must(rows => rows.Length <= 10_000);
        // Row-level validation deliberately happens in the import service so a
        // preview can return every invalid CSV row without echoing raw IDs.
    }
}

public sealed class WarrantyLookupRequestValidator : AbstractValidator<WarrantyLookupRequest>
{
    public WarrantyLookupRequestValidator()
    {
        RuleFor(request => request.Identifier).NotEmpty().MaximumLength(128);
        RuleFor(request => request.IdentifierType).IsInEnum().When(request => request.IdentifierType.HasValue);
    }
}

public sealed class ActivateWarrantyRequestValidator : AbstractValidator<ActivateWarrantyRequest>
{
    public ActivateWarrantyRequestValidator()
    {
        RuleFor(request => request.Identifier).NotEmpty().MaximumLength(128);
        RuleFor(request => request.IdentifierType).IsInEnum().When(request => request.IdentifierType.HasValue);
    }
}

public sealed class AdminWarrantyReasonRequestValidator : AbstractValidator<AdminWarrantyReasonRequest>
{
    public AdminWarrantyReasonRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ReplaceWarrantyRequestValidator : AbstractValidator<ReplaceWarrantyRequest>
{
    public ReplaceWarrantyRequestValidator()
    {
        Include(new AdminWarrantyReasonRequestValidator());
        RuleFor(request => request.ReplacementSerializedProductUnitId).NotEmpty();
    }
}
