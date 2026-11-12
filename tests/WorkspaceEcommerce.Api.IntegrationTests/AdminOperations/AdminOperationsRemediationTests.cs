using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminOperations;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminOperationsRemediationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task Catalogue_LocalizedContractsDimensionsCombinedFiltersAndLiteralSearch_RoundTrip()
    {
        await fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var category = await Success(await client.PostAsJsonAsync("/api/admin/categories", new {
            name = new { vi = "Bàn làm việc", en = "Desks", fr = "Bureau" }, slug = "desks", sortOrder = 0, isActive = true }));
        var categoryId = category["id"]!.GetValue<Guid>();
        var product = await Success(await client.PostAsJsonAsync("/api/admin/products", new {
            categoryId, name = new { vi = "Bàn điện 100%", en = "Standing desk", fr = "Bureau réglable" },
            description = new { vi = "Mô tả", en = "Description" }, slug = "standing-desk", isFeatured = true, isActive = true }));
        var productId = product["id"]!.GetValue<Guid>();
        var variant = await Success(await client.PostAsJsonAsync($"/api/admin/products/{productId}/variants", new {
            sku = "DESK-REGRESSION", name = "Black", price = 101, stockQuantity = 3, isActive = true, weightKg = 2.5m, lengthCm = 40, widthCm = 30, heightCm = 20 }));
        Assert.Equal(2.5m, variant["weightKg"]!.GetValue<decimal>());
        var variantId = variant["id"]!.GetValue<Guid>();
        var updatedVariant = await Success(await client.PutAsJsonAsync($"/api/admin/product-variants/{variantId}", new {
            sku = "DESK-REGRESSION", name = "Black", price = 101, stockQuantity = 3, expectedStockQuantity = 3, isActive = true,
            updateDimensions = true, weightKg = 3m, lengthCm = 40, widthCm = 30, heightCm = 20 }));
        Assert.Equal(3m, updatedVariant["weightKg"]!.GetValue<decimal>());
        updatedVariant = await Success(await client.PutAsJsonAsync($"/api/admin/product-variants/{variantId}", new {
            sku = "DESK-REGRESSION", name = "Black", price = 101, stockQuantity = 3, expectedStockQuantity = 3, isActive = true }));
        Assert.Equal(3m, updatedVariant["weightKg"]!.GetValue<decimal>()); // Older clients preserve dimensions.
        using var invalidWeight = await client.PutAsJsonAsync($"/api/admin/product-variants/{variantId}", new {
            sku = "DESK-REGRESSION", name = "Black", price = 101, stockQuantity = 3, expectedStockQuantity = 3, isActive = true, updateDimensions = true, weightKg = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidWeight.StatusCode);
        await Success(await client.PutAsJsonAsync($"/api/admin/products/{productId}", new {
            categoryId, name = new { vi = "Bàn điện 100% mới", en = "Standing desk", fr = "Bureau réglable" },
            description = new { vi = "Mô tả", en = "Description" }, slug = "standing-desk", isFeatured = true, isActive = true }));
        var list = await Success(await client.GetAsync($"/api/admin/products?search=REGRESSION&categoryId={categoryId}&isActive=true&isFeatured=true&stock=low&minPrice=100&maxPrice=102&sortBy=price-asc"));
        Assert.Single(list["items"]!.AsArray());
        Assert.Equal("Bureau réglable", list["items"]![0]!["name"]!["fr"]!.GetValue<string>());
        var byIds = await Success(await client.GetAsync($"/api/admin/products?ids={productId}&pageSize=100"));
        Assert.Equal(1, byIds["totalCount"]!.GetValue<int>());
        var literal = await Success(await client.GetAsync("/api/admin/products?search=100%25"));
        Assert.Equal(1, literal["totalCount"]!.GetValue<int>());
        var absent = await Success(await client.GetAsync("/api/admin/products?search=NOT%25FOUND"));
        Assert.Empty(absent["items"]!.AsArray());
        using var invalid = await client.GetAsync("/api/admin/products?minPrice=200&maxPrice=100");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var invalidIds = await client.GetAsync("/api/admin/products?ids=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidIds.StatusCode);
        var search = await Success(await client.GetAsync("/api/admin/dashboard/search?search=regression"));
        Assert.Contains(search["items"]!.AsArray(), item => item!["id"]!.GetValue<Guid>() == productId);
        search = await Success(await client.GetAsync("/api/admin/dashboard/search?search=" + Uri.EscapeDataString("Bàn")));
        Assert.Contains(search["items"]!.AsArray(), item => item!["kind"]!.GetValue<string>() == "Products");
        using var anonymous = fixture.CreateClient();
        using var denied = await anonymous.GetAsync("/api/admin/dashboard/search?search=desk");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Import_ConcurrentRetrySkipsExistingWithoutStockOrOutboxSideEffects_AndConflictsAreVisible()
    {
        await fixture.ResetDatabaseAsync(); await SeedCatalogAsync();
        using var first = await AdminAsync(); using var second = await AdminAsync();
        var responses = await Task.WhenAll(ImportAsync(first, "commit", "EXT-1", "Cod"), ImportAsync(second, "commit", "EXT-1", "Cod"));
        var values = new List<JsonNode>();
        foreach (var response in responses) values.Add(await Success(response));
        Assert.Equal(1, values.Sum(value => value["createdOrders"]!.GetValue<int>()));
        Assert.Equal(1, values.Sum(value => value["skippedOrders"]!.GetValue<int>()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(9, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        var repeat = await Success(await ImportAsync(first, "commit", "EXT-1", "Cod"));
        Assert.Equal(1, repeat["skippedOrders"]!.GetValue<int>());
        var conflict = await Success(await ImportAsync(first, "commit", "EXT-1", "Cod", quantity: 2));
        Assert.True(conflict["preview"]!["errorRows"]!.GetValue<int>() > 0);
        Assert.Equal(9, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
        var separate = await Success(await ImportAsync(first, "commit", "EXT-1", "Cod", source: "other"));
        Assert.Equal(1, separate["createdOrders"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("100.5", "0", "Cod")]
    [InlineData("100", "0.5", "Cod")]
    [InlineData("100", "0", "VNPay")]
    public async Task Import_PreviewAndCommitRejectInvalidMoneyOrGatewayWithoutStockChanges(string price, string fee, string method)
    {
        await fixture.ResetDatabaseAsync(); await SeedCatalogAsync(); using var client = await AdminAsync();
        var preview = await Success(await ImportAsync(client, "preview", "INVALID", method, price: price, fee: fee));
        Assert.True(preview["errorRows"]!.GetValue<int>() > 0);
        var commit = await Success(await ImportAsync(client, "commit", "INVALID", method, price: price, fee: fee));
        Assert.Equal(0, commit["createdOrders"]!.GetValue<int>());
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Orders.CountAsync()));
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
    }

    [Fact]
    public async Task ImportedTransfer_HasDeadlineCannotShipUnpaidAndCanConfirmPayment()
    {
        await fixture.ResetDatabaseAsync(); await SeedCatalogAsync(); using var client = await AdminAsync();
        await Success(await ImportAsync(client, "commit", "TRANSFER-1", "ManualBankTransfer"));
        var order = await fixture.ExecuteDbAsync(db => db.Orders.SingleAsync());
        Assert.NotNull(order.PaymentDueAt);
        foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing })
            await Success(await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status, internalNote = "Process imported order" }));
        using var unpaid = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = OrderStatus.Shipping });
        Assert.Equal(HttpStatusCode.Conflict, unpaid.StatusCode);
        await Success(await client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/payment/confirm-transfer", new { amount = order.TotalAmount, currency = "VND", reference = "BANK-1", reason = "Bank receipt verified" }));
        await Success(await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = OrderStatus.Shipping }));
        Assert.Equal(PaymentStatus.Paid, await fixture.ExecuteDbAsync(db => db.Orders.Select(o => o.PaymentStatus).SingleAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
    }

    [Fact]
    public async Task Coupon_AdminUpdateWaitsForUsageLock_AndPreservesNewUsageCount()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync();
        var coupon = new Coupon(Guid.NewGuid(), "LOCKED", "Old name", null, CouponDiscountType.FixedAmount, 1, null, null, null, null, 10);
        await fixture.SeedAsync(db => { db.Add(coupon); return Task.CompletedTask; });
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var usage = fixture.ExecuteDbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var tracked = await db.Coupons.FromSqlInterpolated($"SELECT * FROM promotions.coupons WHERE id = {coupon.Id} FOR UPDATE").SingleAsync();
            tracked.ReserveUsage(); locked.SetResult(); await release.Task;
            await db.SaveChangesAsync(); await transaction.CommitAsync(); return true;
        });
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var update = client.PutAsJsonAsync($"/api/admin/coupons/{coupon.Id}", new { code = "LOCKED", name = "New name", discountType = 1, discountValue = 2, usageLimit = 10, isActive = false, productTargetIds = Array.Empty<Guid>() });
        try { await Task.Delay(150); Assert.False(update.IsCompleted); } finally { release.SetResult(); }
        await Success(await update); await usage;
        var saved = await fixture.ExecuteDbAsync(db => db.Coupons.SingleAsync());
        Assert.Equal(1, saved.UsedCount); Assert.Equal("New name", saved.Name); Assert.False(saved.IsActive);
    }

    [Fact]
    public async Task Returns_PartialReceiptsRefundRoundingPointsDebtAndRetryRemainConsistent()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync();
        var seed = await SeedCompletedAsync(client, quantity: 3, withPoints: true);
        decimal refunded = 0;
        for (var index = 0; index < 3; index++)
        {
            var requestId = Guid.NewGuid();
            var create = new { requestId, reason = "Return accepted", items = new[] { new { orderItemId = seed.ItemId, quantity = 1 } } };
            var summary = await Success(await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns", create));
            var value = summary["returns"]!["items"]![0]!; var id = value["id"]!.GetValue<Guid>();
            await Success(await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns", create));
            var receipt = new { expectedRevision = 1, reason = "Received and checked", refundShipping = index == 2, items = new[] { new { orderItemId = seed.ItemId, receivedQuantity = 1, restockQuantity = index == 0 ? 1 : 0, inspection = "Condition inspected", serializedUnitIds = Array.Empty<Guid>() } } };
            var receiveResponses = await Task.WhenAll(client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/receive", receipt), client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/receive", receipt));
            summary = await Success(receiveResponses[0]); await Success(receiveResponses[1]);
            var queue = await Success(await client.GetAsync("/api/admin/orders?queue=refunds"));
            Assert.Equal(1, queue["totalCount"]!.GetValue<int>());
            value = summary["returns"]!["items"]!.AsArray().Single(v => v!["id"]!.GetValue<Guid>() == id)!;
            var amount = value["refundAmount"]!.GetValue<decimal>(); refunded += amount;
            using var wrong = await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/refund", new { expectedRevision = 2, amount = amount + 1, reference = $"WRONG-{index}", reason = "Proof" });
            Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
            var refund = new { expectedRevision = 2, amount, reference = $"BANK-REFUND-{index}", reason = "Verified outgoing bank evidence" };
            var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/refund", refund), client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/refund", refund));
            foreach (var response in responses) await Success(response);
            var state = await fixture.ExecuteDbAsync(db => db.Orders.SingleAsync());
            Assert.Equal(refunded, state.RefundedAmount);
            Assert.Equal(index < 2 ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded, state.PaymentStatus);
            Assert.Equal(index < 2 ? OrderStatus.Completed : OrderStatus.Returned, state.Status);
            var dashboard = await Success(await client.GetAsync("/api/admin/dashboard"));
            Assert.Equal(300m - refunded, dashboard["netCollectedRevenue"]!.GetValue<decimal>());
        }
        Assert.Equal(300m, refunded); // 3 x 101 - 5 discount + 2 shipping; no rounding loss.
        Assert.Equal(11, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
        Assert.Equal(9, await fixture.ExecuteDbAsync(db => db.OrderReturnAudits.CountAsync()));
        var account = await fixture.ExecuteDbAsync(db => db.CustomerLoyaltyAccounts.SingleAsync());
        Assert.Equal(0, account.CurrentPoints); Assert.Equal(8, account.PointsDebt); Assert.Equal(0, account.TotalPointsEarned);
    }

    [Fact]
    public async Task Return_ConcurrentApprovalsCannotExceedPurchasedQuantity_AndUnpaidIsDenied()
    {
        await fixture.ResetDatabaseAsync(); using var first = await AdminAsync(); using var second = await AdminAsync();
        var seed = await SeedCompletedAsync(first, 1, false);
        var responses = await Task.WhenAll(new[] { first, second }.Select(client => client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns", new { requestId = Guid.NewGuid(), reason = "Inspect", items = new[] { new { orderItemId = seed.ItemId, quantity = 1 } } })));
        Assert.Single(responses, r => r.IsSuccessStatusCode); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.OrderReturns.CountAsync()));
        using var unauthenticated = fixture.CreateClient();
        using var denied = await unauthenticated.GetAsync($"/api/admin/orders/{seed.OrderId}/returns");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Warranty_QuantityOneReplacementRetriesAndSecondReplacement_ConsumeSpareStockOnceEach()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync();
        var seed = await SeedCompletedAsync(client, 1, false); var target = await SeedWarrantyAsync(seed);
        var payload = new { replacementSerializedProductUnitId = target.Spare1, reason = "Defect verified" };
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/admin/warranties/{target.Entitlement}/replace", payload), client.PostAsJsonAsync($"/api/admin/warranties/{target.Entitlement}/replace", payload));
        foreach (var response in responses) await Success(response);
        Assert.Equal(9, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
        var replacement = await fixture.ExecuteDbAsync(db => db.WarrantyEntitlements.SingleAsync(e => e.SerializedProductUnitId == target.Spare1));
        Assert.True(replacement.UpdatedAt >= replacement.CreatedAt);
        await Success(await client.PostAsJsonAsync($"/api/admin/warranties/{replacement.Id}/replace", new { replacementSerializedProductUnitId = target.Spare2, reason = "Second defect verified" }));
        Assert.Equal(8, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
        Assert.Equal(3, await fixture.ExecuteDbAsync(db => db.WarrantyEntitlements.CountAsync()));
        Assert.Equal(2, await fixture.ExecuteDbAsync(db => db.WarrantyAuditEvents.CountAsync(a => a.Action == WarrantyAuditAction.Replaced)));
    }

    [Fact]
    public async Task SerializedReturn_RequiresQuarantineVoidsWarrantyAndRejectsReplacementAfterReceipt()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync();
        var seed = await SeedCompletedAsync(client, 1, false); var target = await SeedWarrantyAsync(seed);
        var summary = await Success(await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns", new { requestId = Guid.NewGuid(), reason = "Return", items = new[] { new { orderItemId = seed.ItemId, quantity = 1 } } }));
        var id = summary["returns"]!["items"]![0]!["id"]!.GetValue<Guid>();
        object Receipt(int restock) => new { expectedRevision = 1, reason = "Inspected", refundShipping = true, items = new[] { new { orderItemId = seed.ItemId, receivedQuantity = 1, restockQuantity = restock, inspection = "Serial and condition verified", serializedUnitIds = new[] { target.OldUnit } } } };
        using var invalid = await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/receive", Receipt(1)); Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        await Success(await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/receive", Receipt(0)));
        Assert.Equal(WarrantyEntitlementStatus.Voided, await fixture.ExecuteDbAsync(db => db.WarrantyEntitlements.Select(e => e.Status).SingleAsync()));
        Assert.Equal(SerializedProductUnitStatus.Returned, await fixture.ExecuteDbAsync(db => db.SerializedProductUnits.Where(u => u.Id == target.OldUnit).Select(u => u.Status).SingleAsync()));
        using var replace = await client.PostAsJsonAsync($"/api/admin/warranties/{target.Entitlement}/replace", new { replacementSerializedProductUnitId = target.Spare1, reason = "Invalid replacement" }); Assert.Equal(HttpStatusCode.Conflict, replace.StatusCode);
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
    }

    [Fact]
    public async Task ConcurrentWarrantyVoidAndReplace_ProduceOneTerminalDecision()
    {
        await fixture.ResetDatabaseAsync(); using var first = await AdminAsync(); using var second = await AdminAsync();
        var seed = await SeedCompletedAsync(first, 1, false); var target = await SeedWarrantyAsync(seed);
        var responses = await Task.WhenAll(first.PostAsJsonAsync($"/api/admin/warranties/{target.Entitlement}/void", new { reason = "Void after review" }),
            second.PostAsJsonAsync($"/api/admin/warranties/{target.Entitlement}/replace", new { replacementSerializedProductUnitId = target.Spare1, reason = "Replace after review" }));
        Assert.Single(responses, response => response.IsSuccessStatusCode);
        Assert.Single(responses, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        foreach (var response in responses) response.Dispose();
        var status = await fixture.ExecuteDbAsync(db => db.WarrantyEntitlements.Where(e => e.Id == target.Entitlement).Select(e => e.Status).SingleAsync());
        Assert.Equal(status == WarrantyEntitlementStatus.Replaced ? 9 : 10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.WarrantyAuditEvents.CountAsync(e => e.WarrantyEntitlementId == target.Entitlement && (e.Action == WarrantyAuditAction.Voided || e.Action == WarrantyAuditAction.Replaced))));
    }

    [Theory]
    [InlineData(80, 20)]
    [InlineData(110, 10)]
    public async Task PaymentDifferences_RequireExactVerifiedSettlementAndConfirmFullPaymentAtomically(int actual, int settlement)
    {
        await fixture.ResetDatabaseAsync(); await SeedCatalogAsync(); using var client = await AdminAsync();
        await Success(await ImportAsync(client, "commit", "PAYMENT-DIFF", "ManualBankTransfer"));
        var orderId = await fixture.ExecuteDbAsync(db => db.Orders.Select(o => o.Id).SingleAsync());
        var request = new { requestId = Guid.NewGuid(), actualAmount = actual, reference = "INCOMING-1", reason = "Checked original bank receipt" };
        var summary = await Success(await client.PostAsJsonAsync($"/api/admin/orders/{orderId}/payment/exceptions", request));
        var exceptionId = summary["exceptions"]!["items"]![0]!["id"]!.GetValue<Guid>();
        await Success(await client.PostAsJsonAsync($"/api/admin/orders/{orderId}/payment/exceptions", request));
        using var premature = await client.PostAsJsonAsync($"/api/admin/orders/{orderId}/payment/confirm-transfer", new { amount = 100, currency = "VND", reference = "INCOMING-1", reason = "Cannot bypass an open difference" });
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        using var wrong = await client.PostAsJsonAsync($"/api/admin/orders/{orderId}/payment/exceptions/{exceptionId}/resolve", new { amount = settlement + 1, reference = "SETTLEMENT-1", reason = "Incorrect amount" });
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        Assert.Equal(PaymentStatus.Pending, await fixture.ExecuteDbAsync(db => db.Orders.Select(o => o.PaymentStatus).SingleAsync()));
        var evidence = new { amount = settlement, reference = "SETTLEMENT-1", reason = "Verified incoming shortfall / outgoing excess payment" };
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/admin/orders/{orderId}/payment/exceptions/{exceptionId}/resolve", evidence), client.PostAsJsonAsync($"/api/admin/orders/{orderId}/payment/exceptions/{exceptionId}/resolve", evidence));
        foreach (var response in responses) await Success(response);
        Assert.Equal(PaymentStatus.Paid, await fixture.ExecuteDbAsync(db => db.Orders.Select(o => o.PaymentStatus).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Set<WorkspaceEcommerce.Domain.Modules.Payments.OrderPaymentReceipt>().CountAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.OrderPaymentExceptions.CountAsync()));
    }

    [Fact]
    public async Task CancelledShortPayment_RequiresReturningActualReceivedFundsAndDoesNotMarkOrderPaid()
    {
        await fixture.ResetDatabaseAsync(); await SeedCatalogAsync(); using var client = await AdminAsync();
        await Success(await ImportAsync(client, "commit", "CANCEL-PARTIAL", "ManualBankTransfer"));
        var id = await fixture.ExecuteDbAsync(db => db.Orders.Select(o => o.Id).SingleAsync());
        var summary = await Success(await client.PostAsJsonAsync($"/api/admin/orders/{id}/payment/exceptions", new { requestId = Guid.NewGuid(), actualAmount = 60, reference = "IN-60", reason = "Partial receipt verified" }));
        var exceptionId = summary["exceptions"]!["items"]![0]!["id"]!.GetValue<Guid>();
        await Success(await client.PutAsJsonAsync($"/api/admin/orders/{id}/status", new { status = OrderStatus.Cancelled, cancellationReason = "Buyer cancelled before complete payment" }));
        summary = await Success(await client.GetAsync($"/api/admin/orders/{id}/payment/exceptions"));
        Assert.Equal(60m, summary["exceptions"]!["items"]![0]!["requiredAmount"]!.GetValue<decimal>());
        await Success(await client.PostAsJsonAsync($"/api/admin/orders/{id}/payment/exceptions/{exceptionId}/resolve", new { amount = 60, reference = "OUT-60", reason = "Returned actual funds to buyer" }));
        Assert.Equal(OrderStatus.Cancelled, await fixture.ExecuteDbAsync(db => db.Orders.Select(o => o.Status).SingleAsync()));
        Assert.NotEqual(PaymentStatus.Paid, await fixture.ExecuteDbAsync(db => db.Orders.Select(o => o.PaymentStatus).SingleAsync()));
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
    }

    [Fact]
    public async Task ManualStockAdjustments_RequireReasonRecordBeforeAfterAndRejectStaleCounts()
    {
        await fixture.ResetDatabaseAsync(); await SeedCatalogAsync(); using var client = await AdminAsync();
        var variant = TestData.CreateVisibleCatalog().Variant;
        object Body(int stock, int expected, string? reason) => new { sku = variant.Sku, name = variant.Name, price = variant.Price, stockQuantity = stock, expectedStockQuantity = expected, stockChangeReason = reason, isActive = true };
        using var missing = await client.PutAsJsonAsync($"/api/admin/product-variants/{variant.Id}", Body(12, 10, null)); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await Success(await client.PutAsJsonAsync($"/api/admin/product-variants/{variant.Id}", Body(12, 10, "Count checked against physical stock")));
        using var stale = await client.PutAsJsonAsync($"/api/admin/product-variants/{variant.Id}", Body(13, 10, "Stale count")); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var history = await Success(await client.GetAsync($"/api/admin/product-variants/{variant.Id}/stock-history"));
        Assert.Equal(1, history["totalCount"]!.GetValue<int>()); Assert.Equal(10, history["items"]![0]!["beforeQuantity"]!.GetValue<int>()); Assert.Equal(12, history["items"]![0]!["afterQuantity"]!.GetValue<int>());
        using var deletion = await client.DeleteAsync($"/api/admin/products/{variant.ProductId}"); Assert.Equal(HttpStatusCode.Conflict, deletion.StatusCode);
    }

    [Fact]
    public async Task ArticlesAndModeration_CanReachItemsBeyondTheOldHundredItemCap()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync();
        var posts = Enumerable.Range(1, 105).Select(i => new BlogPost(Guid.NewGuid(), $"Article {i}", $"article-{i}", "Summary", "Content", null, true)).ToArray();
        var comments = Enumerable.Range(1, 105).Select(i => new BlogComment(Guid.NewGuid(), posts[0].Id, $"Author {i}", "reader@example.com", $"Comment {i}")).ToArray();
        await fixture.SeedAsync(db => { db.AddRange(posts); db.AddRange(comments); return Task.CompletedTask; });
        var page = await Success(await client.GetAsync("/api/admin/blog-posts/page?pageNumber=6&pageSize=20")); Assert.Equal(105, page["totalCount"]!.GetValue<int>()); Assert.Equal(5, page["items"]!.AsArray().Count);
        var queue = await Success(await client.GetAsync("/api/admin/blog-comments/page?status=0&pageNumber=6&pageSize=20")); Assert.Equal(105, queue["totalCount"]!.GetValue<int>()); Assert.Equal(5, queue["items"]!.AsArray().Count);
        var id = queue["items"]![0]!["id"]!.GetValue<Guid>(); await Success(await client.PostAsync($"/api/admin/blog-comments/{id}/approve", null));
        queue = await Success(await client.GetAsync("/api/admin/blog-comments/page?status=0&pageNumber=6&pageSize=20")); Assert.Equal(104, queue["totalCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task ReturnWaitingBehindReplacement_ReloadsInsteadOfMutatingNewUnlockedUnit()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync();
        var seed = await SeedCompletedAsync(client, 1, false); var target = await SeedWarrantyAsync(seed);
        var summary = await Success(await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns", new { requestId = Guid.NewGuid(), reason = "Physical return", items = new[] { new { orderItemId = seed.ItemId, quantity = 1 } } }));
        var id = summary["returns"]!["items"]![0]!["id"]!.GetValue<Guid>();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = fixture.ExecuteDbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.SerializedProductUnits.FromSqlInterpolated($"SELECT * FROM warranty.serialized_product_units WHERE id = {target.OldUnit} FOR UPDATE").SingleAsync();
            locked.SetResult(); await release.Task; await transaction.CommitAsync(); return true;
        });
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var replacement = client.PostAsJsonAsync($"/api/admin/warranties/{target.Entitlement}/replace", new { replacementSerializedProductUnitId = target.Spare1, reason = "Approved replacement" });
        Task<HttpResponseMessage>? receipt = null;
        try {
            await WaitForUnitLockWaitersAsync(1);
            receipt = client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/{id}/receive", new { expectedRevision = 1, reason = "Receive latest unit", refundShipping = true,
                items = new[] { new { orderItemId = seed.ItemId, receivedQuantity = 1, restockQuantity = 0, inspection = "Physical unit checked", serializedUnitIds = new[] { target.Spare1 } } } });
            await WaitForUnitLockWaitersAsync(2);
            Assert.False(replacement.IsCompleted); Assert.False(receipt.IsCompleted);
        } finally { release.SetResult(); }
        await Success(await replacement); using var response = await receipt!;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Physical unit assignments changed", (await response.ReadJsonAsync()).ToJsonString());
        await blocker;
        Assert.Equal(SerializedProductUnitStatus.Activated, await fixture.ExecuteDbAsync(db => db.SerializedProductUnits.Where(u => u.Id == target.Spare1).Select(u => u.Status).SingleAsync()));
        Assert.Equal(OrderReturnStatus.AwaitingReceipt, await fixture.ExecuteDbAsync(db => db.OrderReturns.Select(r => r.Status).SingleAsync()));
    }

    [Fact]
    public async Task ExpiredWarrantyCannotConsumeSpareStock()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync();
        var seed = await SeedCompletedAsync(client, 1, false); var target = await SeedWarrantyAsync(seed, expired: true);
        using var response = await client.PostAsJsonAsync($"/api/admin/warranties/{target.Entitlement}/replace", new { replacementSerializedProductUnitId = target.Spare1, reason = "Expired coverage" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(v => v.StockQuantity).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.WarrantyEntitlements.CountAsync()));
    }

    [Fact]
    public async Task RejectedCustomerRequestCannotBeBypassedByCreatingAnAdminReturnCase()
    {
        await fixture.ResetDatabaseAsync(); using var client = await AdminAsync(); var seed = await SeedCompletedAsync(client, 1, false);
        await fixture.SeedAsync(async db => { var order = await db.Orders.SingleAsync(); order.RequestReturn("Customer request", DateTimeOffset.UtcNow); });
        var summary = await Success(await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns/reject-request", new { reason = "Request reviewed and declined" }));
        Assert.False(summary["canCreate"]!.GetValue<bool>()); Assert.Equal("Rejected", summary["decision"]!.GetValue<string>());
        using var response = await client.PostAsJsonAsync($"/api/admin/orders/{seed.OrderId}/returns", new { requestId = Guid.NewGuid(), reason = "Cannot bypass rejection", items = new[] { new { orderItemId = seed.ItemId, quantity = 1 } } });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.OrderReturns.CountAsync()));
    }

    [Fact]
    public async Task MigrationRollbackCannotEraseStockAuditEvidence()
    {
        await fixture.ResetDatabaseAsync(); await SeedCatalogAsync();
        await fixture.SeedAsync(db => { db.Add(new StockAdjustment(Guid.NewGuid(), TestData.CreateVisibleCatalog().Variant.Id, 10, 12, "Counted stock", "test-admin", DateTimeOffset.UtcNow)); return Task.CompletedTask; });
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteDbAsync(async db => {
            await db.GetService<IMigrator>().MigrateAsync("20261112015210_PaymentDifferenceReconciliation"); return true;
        }));
        Assert.Contains("Stock adjustment evidence exists", error.MessageText);
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.StockAdjustments.CountAsync()));
    }

    private async Task<HttpClient> AdminAsync() { var client = fixture.CreateClient(); client.UseBearerToken(await client.LoginAsAdminAsync()); return client; }
    private async Task WaitForUnitLockWaitersAsync(int expected)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            var waiting = await fixture.ExecuteDbAsync(db => db.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                  AND query LIKE '%serialized_product_units%' AND query LIKE '%FOR UPDATE%'
                """).SingleAsync());
            if (waiting >= expected) return;
            await Task.Delay(25);
        }
        throw new TimeoutException($"Expected {expected} serialized-unit lock waiters.");
    }
    private async Task SeedCatalogAsync() { var c = TestData.CreateVisibleCatalog(); await fixture.SeedAsync(db => { db.AddRange(c.Category, c.Product, c.Variant); return Task.CompletedTask; }); }
    private static async Task<JsonNode> Success(HttpResponseMessage response)
    {
        using (response) { var value = await response.ReadJsonAsync(); Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {value}"); return value["data"]!; }
    }
    private static async Task<HttpResponseMessage> ImportAsync(HttpClient client, string action, string code, string method, int quantity = 1, string price = "100", string fee = "0", string source = "regression")
    {
        using var file = new MultipartFormDataContent(); file.Add(new StringContent(source), "source");
        file.Add(new ByteArrayContent(Encoding.UTF8.GetBytes($"externalOrderCode,customerName,customerPhone,shippingStreet,shippingWard,shippingProvince,paymentMethod,sku,quantity,unitPrice,shippingFee\n{code},Regression Buyer,0900000000,Test Street,Test Ward,Test Province,{method},DESK-001,{quantity},{price},{fee}")), "file", "orders.csv");
        return await client.PostAsync($"/api/admin/orders/import/{action}", file);
    }
    private async Task<ReturnSeed> SeedCompletedAsync(HttpClient client, int quantity, bool withPoints)
    {
        await SeedCatalogAsync(); Guid? customerId = null;
        if (withPoints) { await client.RegisterCustomerAsync(); customerId = await fixture.ExecuteDbAsync(db => db.Customers.Select(c => c.Id).SingleAsync()); }
        var c = TestData.CreateVisibleCatalog(); var order = new Order(Guid.NewGuid(), "RMA-TEST", customerId, "Return Buyer", "0900000000", "customer@example.com", "Test Street", null, PaymentMethod.Cod, "VND", 1);
        var item = order.AddItem(Guid.NewGuid(), c.Variant.Id, "Desk", c.Variant.Sku, 101, quantity, false);
        order.SetShippingFee(2);
        var coupon = new Coupon(Guid.NewGuid(), "RETURN-DISCOUNT", "Discount", null, CouponDiscountType.FixedAmount, 5, null, null, null, null, 10);
        order.ApplyCoupon(coupon.Id, coupon.Code, coupon.Name, 5);
        order.RecordCreated(Guid.NewGuid(), null, "admin"); order.MarkPaymentPaid(DateTimeOffset.UtcNow);
        foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Shipping, OrderStatus.Completed }) order.ChangeStatus(Guid.NewGuid(), status, null, "admin");
        await fixture.SeedAsync(db => {
            db.AddRange(coupon, order);
            if (customerId.HasValue) { var account = new CustomerLoyaltyAccount(Guid.NewGuid(), customerId.Value); account.EarnPoints(10, order.Id, "Earned on sale"); var voucher = Coupon.CreateLoyaltyVoucher(Guid.NewGuid(), customerId.Value, "LOYAL-TEST", "Voucher", 8000, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)); db.Add(voucher); account.RedeemPoints(8, voucher.Id, "Spent before return"); db.Add(account); }
            return Task.CompletedTask;
        });
        return new(order.Id, item.Id, c.Variant.Id);
    }
    private async Task<WarrantySeed> SeedWarrantyAsync(ReturnSeed seed, bool expired = false)
    {
        var now = DateTimeOffset.UtcNow; var plan = new WarrantyPlan(Guid.NewGuid(), "TEST", "Warranty", 60, "v1", now.AddDays(-1), null); plan.AddCoverage(Guid.NewGuid(), "FRAME", "Frame", 60, 0);
        var batch = new WarrantyImportBatch(Guid.NewGuid(), "regression-fingerprint", "admin", 3, now); batch.Complete(3, 0, now);
        SerializedProductUnit Unit(string key) => new(Guid.NewGuid(), seed.VariantId, WarrantyIdentifierType.Serial, 1, key.PadRight(64, '0'), "***" + key, batch.Id, now);
        var original = Unit("001"); var spare1 = Unit("002"); var spare2 = Unit("003"); original.AssignToOrderItem(seed.ItemId, now); original.Activate(now);
        var entitlement = new WarrantyEntitlement(Guid.NewGuid(), original.Id, plan.Id, seed.OrderId, seed.ItemId, null, now);
        var coverageStart = expired ? now.AddYears(-6) : now;
        entitlement.Activate(coverageStart, coverageStart, coverageStart.AddDays(60), coverageStart, WarrantyActivationSource.Admin, "v1", [new WarrantyCoverageSnapshot(Guid.NewGuid(), entitlement.Id, "FRAME", "Frame", 60, coverageStart, coverageStart.AddMonths(60), 0)]);
        await fixture.SeedAsync(db => { db.AddRange(plan, batch, original, spare1, spare2, entitlement); return Task.CompletedTask; });
        return new(entitlement.Id, original.Id, spare1.Id, spare2.Id);
    }
    private sealed record ReturnSeed(Guid OrderId, Guid ItemId, Guid VariantId);
    private sealed record WarrantySeed(Guid Entitlement, Guid OldUnit, Guid Spare1, Guid Spare2);
}
