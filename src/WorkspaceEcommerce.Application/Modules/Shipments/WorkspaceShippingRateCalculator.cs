using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed record ShippingWeightLine(decimal? WeightKg, int Quantity);
public sealed record ShippingRateCalculation(int WeightGrams, int ExtraSteps,
    decimal BaseFee, decimal ExtraWeightFee, decimal TotalFee, string Currency);

public static class WorkspaceShippingRateCalculator
{
    public static Result<ShippingRateCalculation> Calculate(ShippingRateZone zone,
        IReadOnlyCollection<ShippingWeightLine> items)
    {
        if (items.Count is 0 or > 1000)
            return Result<ShippingRateCalculation>.Validation(["Provide between 1 and 1000 weighted lines."]);
        if (items.Any(item => item is null || item.WeightKg is null or <= 0 || item.Quantity <= 0))
            return Result<ShippingRateCalculation>.Validation(["Every line requires a positive actual weight and quantity; no fallback weight is used."]);
        try
        {
            var totalKg = items.Aggregate(0m, (total, item) => checked(total + item.WeightKg!.Value * item.Quantity));
            var roundedGrams = decimal.Ceiling(checked(totalKg * 1000m));
            if (roundedGrams > int.MaxValue)
                return Result<ShippingRateCalculation>.Validation(["Aggregate shipment weight exceeds the supported range."]);
            var grams = (int)roundedGrams;
            if (zone.MaximumWeightGrams.HasValue && grams > zone.MaximumWeightGrams.Value)
                return Result<ShippingRateCalculation>.Validation(["The shipment exceeds this zone's configured maximum weight."]);
            var extra = Math.Max(0L, (long)grams - zone.BaseWeightGrams);
            var steps = (int)((extra + zone.StepWeightGrams - 1) / zone.StepWeightGrams);
            var extraFee = checked(zone.StepFee * steps);
            var fee = checked(zone.BaseFee + extraFee);
            ShippingRateZone.ValidateMoney(fee);
            return Result<ShippingRateCalculation>.Success(new ShippingRateCalculation(
                grams, steps, zone.BaseFee, extraFee, fee, "VND"));
        }
        catch (OverflowException)
        {
            return Result<ShippingRateCalculation>.Validation(["Shipping weight or fee exceeds the supported range."]);
        }
        catch (DomainException exception)
        {
            return Result<ShippingRateCalculation>.Validation([exception.Message]);
        }
    }
}
