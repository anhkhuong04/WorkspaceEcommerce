using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class ShippingRateModelConfigurationTests
{
    [Fact]
    public void Model_EnforcesPublishedUniquenessProvinceCoverageAndSingleUseQuote()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test").Options);
        var plan = context.Model.FindEntityType(typeof(ShippingRatePlan))!;
        Assert.True(plan.FindProperty(nameof(ShippingRatePlan.Revision))!.IsConcurrencyToken);
        Assert.Contains(plan.GetIndexes(), index => index.IsUnique && index.GetFilter() == "status = 'Published'");
        var provinces = context.Model.FindEntityType(typeof(ShippingZoneProvince))!;
        Assert.Contains(provinces.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(["RatePlanId", "ProvinceCode"]));
        var quote = context.Model.FindEntityType(typeof(ShippingQuote))!;
        Assert.True(quote.FindProperty(nameof(ShippingQuote.Revision))!.IsConcurrencyToken);
        Assert.Contains(quote.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "OrderId");
        var fee = context.Model.FindEntityType(typeof(ShippingRateZone))!.FindProperty(nameof(ShippingRateZone.BaseFee))!;
        Assert.Equal(18, fee.GetPrecision());
        Assert.Equal(0, fee.GetScale());
        Assert.True(context.Model.FindEntityType(typeof(OrderShipment))!
            .FindProperty(nameof(OrderShipment.ProviderShipmentId))!.IsNullable);
    }
}
