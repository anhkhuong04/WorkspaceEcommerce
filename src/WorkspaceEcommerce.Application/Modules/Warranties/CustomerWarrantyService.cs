using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Warranties;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

internal sealed class CustomerWarrantyService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    IWarrantyIdentifierProtector identifierProtector,
    ICustomerEmailOutbox customerEmailOutbox,
    WarrantyOptions options,
    TimeProvider timeProvider,
    IValidator<ActivateWarrantyRequest> activateValidator) : ICustomerWarrantyService
{
    private readonly WarrantyActivationCoordinator activationCoordinator = new(
        dbContext,
        customerEmailOutbox,
        timeProvider);

    public async Task<Result<CustomerWarrantyDto>> ActivateAsync(
        ActivateWarrantyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsActivationEnabled())
        {
            return Result<CustomerWarrantyDto>.NotFound("Warranty activation is unavailable.");
        }

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerWarrantyDto>.Unauthorized("Customer authentication is required.");
        }

        var validation = await activateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CustomerWarrantyDto>.Validation(validation.Errors.Select(error => error.ErrorMessage));
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
        catch (DomainException exception)
        {
            WarrantyMetrics.RecordActivation("rejected", "customer");
            return Result<CustomerWarrantyDto>.Validation([exception.Message]);
        }

        var activation = await activationCoordinator.ActivateForCustomerAsync(
            customerId.Value,
            identifier.IdentifierType,
            fingerprintsByKeyVersion,
            cancellationToken);
        if (activation.IsFailure)
        {
            WarrantyMetrics.RecordActivation("rejected", "customer");
            return ToCustomerActivationFailure(activation);
        }

        var activationOutcome = activation.Value!;
        var result = await GetWarrantyAsync(activationOutcome.Entitlement.Id, cancellationToken);
        WarrantyMetrics.RecordActivation(
            result.IsSuccess
                ? activationOutcome.WasAlreadyActive ? "idempotent" : "activated"
                : "rejected",
            "customer");
        return result;
    }

    public async Task<Result<PagedResult<CustomerWarrantyListItemDto>>> GetWarrantiesAsync(
        CustomerWarrantyListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsActivationEnabled())
        {
            return Result<PagedResult<CustomerWarrantyListItemDto>>.NotFound("Warranty service is unavailable.");
        }

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<PagedResult<CustomerWarrantyListItemDto>>.Unauthorized("Customer authentication is required.");
        }

        var query = dbContext.WarrantyEntitlements
            .AsNoTrackingIfEf()
            .Where(entitlement => entitlement.CustomerId == customerId.Value);
        var totalCount = await query.CountAsyncSafe(cancellationToken);
        var items = await query
            .OrderByDescending(entitlement => entitlement.ActivatedAt)
            .ThenByDescending(entitlement => entitlement.CreatedAt)
            .ThenBy(entitlement => entitlement.Id)
            .Skip(request.Skip)
            .Take(request.NormalizedPageSize)
            .Select(entitlement => new CustomerWarrantyListItemDto(
                entitlement.Id,
                dbContext.OrderItems.Where(item => item.Id == entitlement.OrderItemId).Select(item => item.ProductNameSnapshot).FirstOrDefault() ?? string.Empty,
                dbContext.SerializedProductUnits.Where(unit => unit.Id == entitlement.SerializedProductUnitId).Select(unit => unit.MaskedIdentifier).FirstOrDefault() ?? string.Empty,
                dbContext.SerializedProductUnits.Where(unit => unit.Id == entitlement.SerializedProductUnitId).Select(unit => unit.IdentifierType).FirstOrDefault(),
                entitlement.Status,
                entitlement.ActivatedAt,
                dbContext.WarrantyCoverageSnapshots.Where(snapshot => snapshot.WarrantyEntitlementId == entitlement.Id).Max(snapshot => (DateTimeOffset?)snapshot.EndsAt)))
            .ToArrayAsyncSafe(cancellationToken);
        return Result<PagedResult<CustomerWarrantyListItemDto>>.Success(new PagedResult<CustomerWarrantyListItemDto>(
            items,
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            totalCount));
    }

    public async Task<Result<CustomerWarrantyDto>> GetWarrantyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!IsActivationEnabled())
        {
            return Result<CustomerWarrantyDto>.NotFound("Warranty service is unavailable.");
        }

        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<CustomerWarrantyDto>.Unauthorized("Customer authentication is required.");
        }

        var entitlement = await dbContext.WarrantyEntitlements
            .AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == id && candidate.CustomerId == customerId.Value)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (entitlement is null)
        {
            return Result<CustomerWarrantyDto>.NotFound("Warranty was not found.");
        }

        var unit = await dbContext.SerializedProductUnits.AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == entitlement.SerializedProductUnitId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        var plan = await dbContext.WarrantyPlans.AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == entitlement.WarrantyPlanId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        var item = await dbContext.OrderItems.AsNoTrackingIfEf()
            .Where(candidate => candidate.Id == entitlement.OrderItemId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (unit is null || plan is null || item is null)
        {
            return Result<CustomerWarrantyDto>.Validation(["Warranty data is incomplete."]);
        }

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
        return Result<CustomerWarrantyDto>.Success(new CustomerWarrantyDto(
            entitlement.Id,
            item.ProductNameSnapshot,
            unit.MaskedIdentifier,
            unit.IdentifierType,
            plan.Name,
            entitlement.Status,
            entitlement.PurchasedAt,
            entitlement.ActivationDeadline,
            entitlement.ActivatedAt,
            coverages));
    }

    private bool IsActivationEnabled() => options.Enabled && options.ActivationEnabled;

    private static Result<CustomerWarrantyDto> ToCustomerActivationFailure(
        Result<WarrantyActivationOutcome> result) =>
        result.Status switch
        {
            ResultStatus.Validation => Result<CustomerWarrantyDto>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CustomerWarrantyDto>.NotFound(result.FirstError ?? "Warranty is not available for activation."),
            ResultStatus.Conflict => Result<CustomerWarrantyDto>.Conflict(result.FirstError ?? "Warranty activation changed concurrently."),
            _ => Result<CustomerWarrantyDto>.Failure(result.Errors)
        };
}
