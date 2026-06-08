using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Seeding;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Api.IntegrationTests.DemoData;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class DemoDataSeedIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task DemoDataSeeder_SeedsExpectedDataAndIsIdempotent()
    {
        await fixture.ResetDatabaseAsync();

        var firstResult = await fixture.ExecuteScopeAsync(async services =>
        {
            var seeder = services.GetRequiredService<IDemoDataSeeder>();

            return await seeder.SeedAsync();
        });
        var secondResult = await fixture.ExecuteScopeAsync(async services =>
        {
            var seeder = services.GetRequiredService<IDemoDataSeeder>();

            return await seeder.SeedAsync();
        });

        Assert.Equal(3, firstResult.Categories);
        Assert.Equal(4, firstResult.Products);
        Assert.Equal(5, firstResult.Variants);
        Assert.Equal(3, firstResult.Banners);
        Assert.Equal(1, firstResult.Carts);
        Assert.Equal(3, firstResult.Orders);
        Assert.Equal(0, secondResult.Categories);
        Assert.Equal(0, secondResult.Products);
        Assert.Equal(0, secondResult.Variants);
        Assert.Equal(0, secondResult.Banners);
        Assert.Equal(0, secondResult.Carts);
        Assert.Equal(0, secondResult.Orders);

        var snapshot = await fixture.ExecuteDbAsync(async dbContext => new
        {
            CategoryCount = await dbContext.Categories.CountAsync(),
            ProductCount = await dbContext.Products.CountAsync(),
            VariantCount = await dbContext.ProductVariants.CountAsync(),
            BannerCount = await dbContext.Banners.CountAsync(),
            CheckoutCartItemCount = await dbContext.Carts
                .Where(cart => cart.SessionId == "demo-checkout-session")
                .SelectMany(cart => cart.Items)
                .CountAsync(),
            DemoOrderCount = await dbContext.Orders.CountAsync(order => order.OrderCode.StartsWith("ORD-DEMO-")),
            CompletedOrderTotal = await dbContext.Orders
                .Where(order => order.OrderCode == "ORD-DEMO-COMPLETED")
                .Select(order => order.TotalAmount)
                .SingleAsync()
        });

        Assert.Equal(3, snapshot.CategoryCount);
        Assert.Equal(4, snapshot.ProductCount);
        Assert.Equal(5, snapshot.VariantCount);
        Assert.Equal(3, snapshot.BannerCount);
        Assert.Equal(2, snapshot.CheckoutCartItemCount);
        Assert.Equal(3, snapshot.DemoOrderCount);
        Assert.Equal(457m, snapshot.CompletedOrderTotal);
    }
}
