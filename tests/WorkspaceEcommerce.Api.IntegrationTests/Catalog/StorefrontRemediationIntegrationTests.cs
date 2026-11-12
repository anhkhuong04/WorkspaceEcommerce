using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Api.IntegrationTests.Catalog;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class StorefrontRemediationIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task DirectReturnedStatus_CannotBypassPhysicalInspectionOrFinancialReversal()
    {
        await fixture.ResetDatabaseAsync(); var catalog = TestData.CreateVisibleCatalog();
        var order = TestData.CreatePendingOrder(catalog.Variant.Id);
        foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Shipping, OrderStatus.Completed })
            order.ChangeStatus(Guid.NewGuid(), status, "Test lifecycle", "test");
        order.MarkPaymentPaid(DateTimeOffset.UtcNow);
        await fixture.SeedAsync(db => { db.AddRange(catalog.Category, catalog.Product, catalog.Variant, order); return Task.CompletedTask; });
        using var client = fixture.CreateClient(); client.UseBearerToken(await client.LoginAsAdminAsync());
        using var response = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)OrderStatus.Returned, internalNote = "Requested return" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var persisted = await fixture.ExecuteDbAsync(db => db.Orders.SingleAsync());
        Assert.Equal(OrderStatus.Completed, persisted.Status); Assert.Equal(PaymentStatus.Paid, persisted.PaymentStatus);
        Assert.Equal(order.TotalAmount, persisted.TotalAmount);
    }

    [Fact]
    public async Task StockUpdate_RejectsAStaleAdminForm_AndLockedCheckoutReadRefreshesTrackedStock()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        await fixture.SeedAsync(db => { db.AddRange(catalog.Category, catalog.Product, catalog.Variant); return Task.CompletedTask; });
        await fixture.ExecuteDbAsync(async db =>
        {
            var stale = await db.ProductVariants.SingleAsync();
            Assert.Equal(10, stale.StockQuantity);
            await fixture.SeedAsync(async other => { (await other.ProductVariants.SingleAsync()).DecreaseStock(1); });
            await using var transaction = await db.Database.BeginTransactionAsync();
            var current = Assert.Single(await ((ICheckoutStore)db).FindProductVariantsForUpdateAsync([stale.Id], default));
            Assert.Same(stale, current);
            Assert.Equal(9, current.StockQuantity);
            current.DecreaseStock(1);
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            return true;
        });
        using var client = fixture.CreateClient(); client.UseBearerToken(await client.LoginAsAdminAsync());
        using var response = await client.PutAsJsonAsync($"/api/admin/product-variants/{catalog.Variant.Id}", new
        {
            catalog.Variant.Sku, catalog.Variant.Name, catalog.Variant.Color, catalog.Variant.Size,
            catalog.Variant.Price, catalog.Variant.CompareAtPrice, stockQuantity = 20, expectedStockQuantity = 10,
            catalog.Variant.RequiresInstallation, catalog.Variant.IsActive
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(8, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(variant => variant.StockQuantity).SingleAsync()));
    }

    [Fact]
    public async Task ConcurrentReviews_PreserveAggregate_AndDuplicateCustomerReceivesConflict()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        using var first = fixture.CreateClient(); using var second = fixture.CreateClient();
        first.UseBearerToken(await first.RegisterCustomerAsync(email: "review-a@example.test"));
        second.UseBearerToken(await second.RegisterCustomerAsync(email: "review-b@example.test"));
        await fixture.SeedAsync(async db =>
        {
            db.AddRange(catalog.Category, catalog.Product, catalog.Variant);
            foreach (var customer in await db.Customers.ToArrayAsync())
            {
                var order = new Order(Guid.NewGuid(), $"REVIEW-{customer.Id:N}", customer.Id, customer.FullName,
                    "0900000000", customer.Email, "Test address", null, PaymentMethod.Cod, "VND", 1m);
                order.AddItem(Guid.NewGuid(), catalog.Variant.Id, "Desk", catalog.Variant.Sku, 100_000m, 1, false);
                foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Shipping })
                    order.ChangeStatus(Guid.NewGuid(), status, "Test lifecycle", "test");
                db.Add(order);
            }
        });
        var path = $"/api/products/{catalog.Product.Slug}/reviews";
        var responses = await Task.WhenAll(first.PostAsJsonAsync(path, new { rating = 5, comment = "A" }), second.PostAsJsonAsync(path, new { rating = 1, comment = "B" }));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); response.Dispose(); }
        var aggregate = await fixture.ExecuteDbAsync(db => db.Products.Select(product => new { product.ReviewCount, product.AverageRating }).SingleAsync());
        Assert.Equal(2, aggregate.ReviewCount); Assert.Equal(3d, aggregate.AverageRating);
        using var duplicate = await first.PostAsJsonAsync(path, new { rating = 3, comment = "Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
