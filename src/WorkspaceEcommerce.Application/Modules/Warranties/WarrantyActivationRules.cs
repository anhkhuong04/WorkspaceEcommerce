using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

internal sealed record WarrantyEligibility(
    DateTimeOffset PurchasedAt,
    DateTimeOffset EligibleAt,
    DateTimeOffset ActivationDeadline);

internal static class WarrantyActivationRules
{
    public static WarrantyEligibility GetEligibility(Order order, WarrantyPlan plan, DateTimeOffset now)
    {
        if (order.Status != OrderStatus.Completed || order.CompletedAt is null)
        {
            throw new DomainException("Warranty can be activated after the order is completed.");
        }

        var purchasedAt = order.PaymentMethod == PaymentMethod.Cod
            ? order.CompletedAt.Value
            : order.PaidAt ?? throw new DomainException("A paid timestamp is required before warranty activation.");
        var activationDeadline = purchasedAt.AddDays(plan.ActivationWindowDays);
        if (now > activationDeadline)
        {
            throw new DomainException("The warranty activation window has expired.");
        }

        return new WarrantyEligibility(purchasedAt, order.CompletedAt.Value, activationDeadline);
    }

    public static WarrantyCoverageSnapshot[] CreateCoverageSnapshots(
        WarrantyEntitlement entitlement,
        IEnumerable<WarrantyPlanCoverage> coverages,
        DateTimeOffset coverageStartsAt)
    {
        return coverages
            .OrderBy(coverage => coverage.SortOrder)
            .ThenBy(coverage => coverage.ComponentCode)
            .Select(coverage => new WarrantyCoverageSnapshot(
                Guid.NewGuid(),
                entitlement.Id,
                coverage.ComponentCode,
                coverage.DisplayName,
                coverage.DurationMonths,
                coverageStartsAt,
                coverageStartsAt.AddMonths(coverage.DurationMonths),
                coverage.SortOrder))
            .ToArray();
    }
}
