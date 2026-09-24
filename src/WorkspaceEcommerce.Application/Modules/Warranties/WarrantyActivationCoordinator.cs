using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

internal sealed class WarrantyActivationCoordinator(
    IAppDbContext dbContext,
    ICustomerEmailOutbox customerEmailOutbox,
    TimeProvider timeProvider)
{
    public Task<Result<WarrantyActivationOutcome>> ActivateForAdminAsync(
        Guid entitlementId,
        string actorId,
        CancellationToken cancellationToken) =>
        ActivateAsync(
            transactionCancellationToken => dbContext.FindSerializedProductUnitByEntitlementIdForUpdateAsync(
                entitlementId,
                transactionCancellationToken),
            expectedEntitlementId: entitlementId,
            customerId: null,
            WarrantyActivationSource.Admin,
            actorType: "Admin",
            actorId,
            cancellationToken);

    public Task<Result<WarrantyActivationOutcome>> ActivateForCustomerAsync(
        Guid customerId,
        WarrantyIdentifierType identifierType,
        IReadOnlyDictionary<int, string> fingerprintsByKeyVersion,
        CancellationToken cancellationToken) =>
        ActivateAsync(
            async transactionCancellationToken =>
            {
                foreach (var (keyVersion, fingerprint) in fingerprintsByKeyVersion)
                {
                    var unit = await dbContext.FindSerializedProductUnitForUpdateAsync(
                        identifierType,
                        keyVersion,
                        fingerprint,
                        transactionCancellationToken);
                    if (unit is not null)
                    {
                        return unit;
                    }
                }

                return null;
            },
            expectedEntitlementId: null,
            customerId,
            WarrantyActivationSource.Customer,
            actorType: "Customer",
            customerId.ToString("D"),
            cancellationToken);

    private async Task<Result<WarrantyActivationOutcome>> ActivateAsync(
        Func<CancellationToken, Task<SerializedProductUnit?>> lockUnit,
        Guid? expectedEntitlementId,
        Guid? customerId,
        WarrantyActivationSource source,
        string actorType,
        string actorId,
        CancellationToken cancellationToken)
    {
        Result<WarrantyActivationOutcome>? outcome = null;

        try
        {
            await dbContext.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var unit = await lockUnit(transactionCancellationToken);
                if (unit is null)
                {
                    outcome = NotFound(source);
                    return;
                }

                var entitlement = await dbContext.FindWarrantyEntitlementByUnitIdForUpdateAsync(
                    unit.Id,
                    transactionCancellationToken);
                if (entitlement is null ||
                    expectedEntitlementId.HasValue && entitlement.Id != expectedEntitlementId.Value ||
                    customerId.HasValue && entitlement.CustomerId != customerId.Value)
                {
                    outcome = NotFound(source);
                    return;
                }

                if (entitlement.Status == WarrantyEntitlementStatus.Active)
                {
                    outcome = Result<WarrantyActivationOutcome>.Success(new WarrantyActivationOutcome(
                        entitlement,
                        WasAlreadyActive: true));
                    return;
                }

                if (entitlement.Status != WarrantyEntitlementStatus.PendingActivation)
                {
                    outcome = Result<WarrantyActivationOutcome>.Validation(["Warranty is not available for activation."]);
                    return;
                }

                var order = await dbContext.FindOrderForUpdateAsync(
                    entitlement.OrderId,
                    transactionCancellationToken);
                var plan = await dbContext.WarrantyPlans
                    .Where(candidate => candidate.Id == entitlement.WarrantyPlanId)
                    .FirstOrDefaultAsyncSafe(transactionCancellationToken);
                if (order is null || plan is null ||
                    customerId.HasValue && order.CustomerId != customerId.Value)
                {
                    outcome = source == WarrantyActivationSource.Customer
                        ? NotFound(source)
                        : Result<WarrantyActivationOutcome>.Validation(["Warranty activation data is incomplete."]);
                    return;
                }

                var coverages = await dbContext.WarrantyPlanCoverages
                    .Where(coverage => coverage.WarrantyPlanId == plan.Id)
                    .OrderBy(coverage => coverage.SortOrder)
                    .ThenBy(coverage => coverage.ComponentCode)
                    .ToArrayAsyncSafe(transactionCancellationToken);
                var now = timeProvider.GetUtcNow();
                var eligibility = WarrantyActivationRules.GetEligibility(order, plan, now);
                var snapshots = WarrantyActivationRules.CreateCoverageSnapshots(
                    entitlement,
                    coverages,
                    eligibility.PurchasedAt);
                entitlement.Activate(
                    eligibility.PurchasedAt,
                    eligibility.EligibleAt,
                    eligibility.ActivationDeadline,
                    now,
                    source,
                    plan.TermsVersion,
                    snapshots);
                unit.Activate(now);
                dbContext.Update(entitlement);
                dbContext.Update(unit);
                foreach (var snapshot in snapshots)
                {
                    dbContext.Add(snapshot);
                }

                dbContext.Add(new WarrantyAuditEvent(
                    Guid.NewGuid(),
                    entitlement.Id,
                    unit.Id,
                    WarrantyAuditAction.Activated,
                    actorType,
                    actorId,
                    reason: null,
                    Guid.NewGuid().ToString("N"),
                    now));
                QueueActivationEmail(order.CustomerEmail, unit.MaskedIdentifier, plan.Name, coverages);
                await dbContext.SaveChangesAsync(transactionCancellationToken);
                outcome = Result<WarrantyActivationOutcome>.Success(new WarrantyActivationOutcome(
                    entitlement,
                    WasAlreadyActive: false));
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<WarrantyActivationOutcome>.Validation([exception.Message]);
        }
        catch (PersistenceConcurrencyException)
        {
            return Result<WarrantyActivationOutcome>.Conflict("Warranty activation changed concurrently. Please retry.");
        }

        return outcome ?? Result<WarrantyActivationOutcome>.Failure("Warranty activation did not complete.");
    }

    private static Result<WarrantyActivationOutcome> NotFound(WarrantyActivationSource source) =>
        Result<WarrantyActivationOutcome>.NotFound(source == WarrantyActivationSource.Customer
            ? "Warranty is not available for activation."
            : "Warranty entitlement was not found.");

    private void QueueActivationEmail(
        string? recipientEmail,
        string maskedIdentifier,
        string planName,
        IReadOnlyCollection<WarrantyPlanCoverage> coverages)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return;
        }

        var coverageText = string.Join(", ", coverages
            .OrderBy(coverage => coverage.SortOrder)
            .Select(coverage => $"{coverage.DisplayName}: {coverage.DurationMonths} months"));
        customerEmailOutbox.Enqueue(new CustomerEmailMessage(
            recipientEmail,
            "Your product warranty is active",
            $"Your warranty for {maskedIdentifier} is active under plan {planName}. Coverage: {coverageText}."));
    }
}

internal sealed record WarrantyActivationOutcome(
    WarrantyEntitlement Entitlement,
    bool WasAlreadyActive);
