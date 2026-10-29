using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Tests.Modules.Shipments;

public sealed class WorkspaceShippingRateCalculatorTests
{
    [Theory]
    [InlineData("0.999", 999, 0, 10000)]
    [InlineData("1", 1000, 0, 10000)]
    [InlineData("1.0001", 1001, 1, 15000)]
    [InlineData("1.5", 1500, 1, 15000)]
    [InlineData("1.5001", 1501, 2, 20000)]
    public void ActualWeight_IsRoundedUpOnceAndChargedByWholeSteps(string kg, int grams, int steps, decimal expectedFee)
    {
        var weight = decimal.Parse(kg, System.Globalization.CultureInfo.InvariantCulture);
        var result = WorkspaceShippingRateCalculator.Calculate(Zone(), [new(weight, 1)]);
        Assert.True(result.IsSuccess);
        Assert.Equal(grams, result.Value!.WeightGrams);
        Assert.Equal(steps, result.Value.ExtraSteps);
        Assert.Equal(expectedFee, result.Value.TotalFee);
    }

    [Fact]
    public void QuantitiesAndFractionalGrams_AreAggregatedBeforeRounding()
    {
        var result = WorkspaceShippingRateCalculator.Calculate(Zone(), [new(0.0001m, 3), new(0.0002m, 2)]);
        Assert.Equal(1, result.Value!.WeightGrams);
        Assert.Equal(10000, result.Value.TotalFee);
        Assert.Equal(result.Value, WorkspaceShippingRateCalculator.Calculate(Zone(), [new(0.0001m, 3), new(0.0002m, 2)]).Value);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("0", 1)]
    [InlineData("-1", 1)]
    [InlineData("1", 0)]
    [InlineData("1", -1)]
    public void MissingOrInvalidWeightQuantity_HasNoSilentFallback(string? weight, int quantity)
    {
        decimal? kg = weight is null ? null : decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(WorkspaceShippingRateCalculator.Calculate(Zone(), [new(kg, quantity)]).IsFailure);
    }

    [Fact]
    public void RangeAndCostOverflow_AreRejectedWithoutThrowingOrReturningFreeShipping()
    {
        Assert.True(WorkspaceShippingRateCalculator.Calculate(Zone(), [new(decimal.MaxValue, 2)]).IsFailure);
        Assert.True(WorkspaceShippingRateCalculator.Calculate(Zone(), [new(2147484m, 1)]).IsFailure);
        Assert.True(WorkspaceShippingRateCalculator.Calculate(Zone(maximum: 2000), [new(2.001m, 1)]).IsFailure);
        var expensive = new ShippingRateZone(Guid.NewGuid(), Guid.NewGuid(), "cost", "Cost", 1,
            999_999_999_999_999_999m, 1, 1, null, ["01"]);
        Assert.True(WorkspaceShippingRateCalculator.Calculate(expensive, [new(0.002m, 1)]).IsFailure);
        Assert.True(WorkspaceShippingRateCalculator.Calculate(Zone(), []).IsFailure);
    }

    [Fact]
    public void ZeroFee_IsOnlyReturnedWhenExplicitlyConfigured()
    {
        var free = new ShippingRateZone(Guid.NewGuid(), Guid.NewGuid(), "free", "Free", 1000, 0, 1000, 0, null, ["01"]);
        Assert.Equal(0, WorkspaceShippingRateCalculator.Calculate(free, [new(5m, 1)]).Value!.TotalFee);
    }

    private static ShippingRateZone Zone(int? maximum = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "zone", "Zone", 1000, 10000, 500, 5000, maximum, ["01"]);
}
