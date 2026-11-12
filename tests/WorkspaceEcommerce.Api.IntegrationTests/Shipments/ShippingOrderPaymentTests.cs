using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Payments;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

public sealed partial class ShippingCheckoutIntegrationTests
{
    [Fact]
    public async Task LiveRefreshReturningAfterCancellation_CannotOverwriteTheCancelledOrder()
    {
        await SeedAsync(); var carrier = new CountingCarrier { Available = true };
        await using var factory = Factory(carrier); using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { Method = ShippingMethod.MiniLogistics });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderShipmentService>().ProcessDueCommandsAsync(10);
        var shipment = await fixture.ExecuteDbAsync(db => db.OrderShipments.SingleAsync());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        carrier.Tracking = async () =>
        {
            started.SetResult(); await release.Task;
            return new WorkspaceEcommerce.Application.Abstractions.Shipment.TrackingResponse
            { TrackingCode = shipment.TrackingCode, ExternalOrderId = order.OrderCode, Status = "InTransit", ShippingFeeAmount = shipment.ShippingFeeAmount,
              Currency = "VND", Timeline = [new() { Status = "InTransit", ChangedAtUtc = DateTimeOffset.UtcNow }] };
        };
        using var refreshScope = factory.Services.CreateScope();
        var refreshing = refreshScope.ServiceProvider.GetRequiredService<IOrderShipmentService>().RefreshAsync(order.Id);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var cancelled = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)OrderStatus.Cancelled, cancellationReason = "Concurrent cancellation" });
        Assert.True(cancelled.IsSuccessStatusCode);
        release.SetResult(); Assert.True((await refreshing).IsSuccess);
        var persisted = await fixture.ExecuteDbAsync(db => db.Orders.SingleAsync());
        Assert.Equal(OrderStatus.Cancelled, persisted.Status); Assert.Equal(PaymentStatus.Cancelled, persisted.PaymentStatus);
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(row => row.StockQuantity).SingleAsync()));
    }

    [Fact]
    public async Task Expiry_ReleasesStockAndCouponOnceAcrossConcurrentWorkers_WithoutChangingOrderSnapshots()
    {
        await SeedAsync(); await using var factory = Factory(new CountingCarrier());
        using var client = factory.CreateClient();
        var coupon = new WorkspaceEcommerce.Domain.Modules.Coupons.Coupon(Guid.NewGuid(), "EXPIRY-COUPON", "Expiry test", null,
            WorkspaceEcommerce.Domain.Modules.Coupons.CouponDiscountType.FixedAmount, 1000m, null, null, null, null, 1);
        await fixture.SeedAsync(db => { db.Add(coupon); return Task.CompletedTask; });
        var quote = await QuoteAsync(client, Input() with { PaymentMethod = PaymentMethod.ManualBankTransfer, CouponCode = coupon.Code });
        var checkoutBody = System.Text.Json.JsonSerializer.SerializeToNode(Accept(quote, payment: PaymentMethod.ManualBankTransfer), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        checkoutBody["couponCode"] = coupon.Code;
        using var placed = await client.PostAsJsonAsync("/api/checkout", checkoutBody);
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        Assert.NotNull(order.PaymentDueAt);
        await fixture.ExecuteDbAsync(async db => { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ordering.orders SET payment_due_at = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE id = {order.Id}"); return true; });
        async Task<int> Sweep() { using var scope = factory.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<IOrderExpiryService>().ExpireDueAsync(10, default); }
        Assert.Equal(1, (await Task.WhenAll(Sweep(), Sweep())).Sum());
        Assert.Equal(0, await Sweep());
        var persisted = await fixture.ExecuteDbAsync(db => db.Orders.SingleAsync());
        Assert.Equal(OrderStatus.Cancelled, persisted.Status); Assert.Equal(order.TotalAmount, persisted.TotalAmount);
        Assert.Equal(order.DiscountAmount, persisted.DiscountAmount);
        Assert.Equal(10, await fixture.ExecuteDbAsync(db => db.ProductVariants.Select(row => row.StockQuantity).SingleAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.Coupons.Select(row => row.UsedCount).SingleAsync()));
        Assert.NotNull(await fixture.ExecuteDbAsync(db => db.CouponRedemptions.Select(row => row.ReleasedAt).SingleAsync()));
    }

    [Fact]
    public async Task FullRefund_IsExactAuditedAndIdempotent_AndDoesNotTriggerProviderTransfer()
    {
        await SeedAsync(); await using var factory = Factory(new CountingCarrier());
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { PaymentMethod = PaymentMethod.ManualBankTransfer });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote, payment: PaymentMethod.ManualBankTransfer));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        client.UseBearerToken(await client.LoginAsAdminAsync());
        var root = $"/api/admin/orders/{order.Id}";
        using var transfer = await client.PostAsJsonAsync($"{root}/payment/confirm-transfer", new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "REFUND-CAPTURE", "Statement verified"));
        Assert.True(transfer.IsSuccessStatusCode);
        using var cancelled = await client.PutAsJsonAsync($"{root}/status", new { status = (int)OrderStatus.Cancelled, cancellationReason = "Customer cancellation" });
        Assert.True(cancelled.IsSuccessStatusCode);
        var request = new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "REFUND-PAID-001", "Outgoing bank transfer verified");
        using var partial = await client.PostAsJsonAsync($"{root}/payment/confirm-full-refund", request with { Amount = order.TotalAmount - 1 });
        Assert.Equal(HttpStatusCode.Conflict, partial.StatusCode);
        using var refunded = await client.PostAsJsonAsync($"{root}/payment/confirm-full-refund", request);
        Assert.Equal(PaymentStatus.Refunded, (await DataAsync<OrderPaymentSummaryDto>(refunded)).PaymentStatus);
        using var repeated = await client.PostAsJsonAsync($"{root}/payment/confirm-full-refund", request);
        Assert.True(repeated.IsSuccessStatusCode);
        using var conflicting = await client.PostAsJsonAsync($"{root}/payment/confirm-full-refund", request with { Reference = "OTHER" });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Set<OrderPaymentReceipt>().CountAsync(receipt => receipt.Kind == OrderPaymentReceiptKind.FullRefund)));
        Assert.Equal(OrderStatus.Cancelled, await fixture.ExecuteDbAsync(db => db.Orders.Select(row => row.Status).SingleAsync()));
    }

    [Fact]
    public async Task TransferConfirmation_IsAdminOnlyExactAuditedAndConcurrentIdempotent()
    {
        await SeedAsync();
        var carrier = new CountingCarrier();
        await using var factory = Factory(carrier);
        using var client = factory.CreateClient(); using var second = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { PaymentMethod = PaymentMethod.ManualBankTransfer });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote, payment: PaymentMethod.ManualBankTransfer));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        var path = $"/api/admin/orders/{order.Id}/payment/confirm-transfer";
        var request = new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "SYNTHETIC-BANK-001", "Verified bank statement");
        using var anonymous = await client.PostAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.UseBearerToken(await client.RegisterCustomerAsync());
        using var customer = await client.PostAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        var admin = await client.LoginAsAdminAsync(); client.UseBearerToken(admin); second.UseBearerToken(admin);
        using var partial = await client.PostAsJsonAsync(path, request with { Amount = order.TotalAmount - 1 }); Assert.Equal(HttpStatusCode.Conflict, partial.StatusCode);
        using var excess = await client.PostAsJsonAsync(path, request with { Amount = order.TotalAmount + 1 }); Assert.Equal(HttpStatusCode.Conflict, excess.StatusCode);
        using var currency = await client.PostAsJsonAsync(path, request with { Currency = "USD" }); Assert.Equal(HttpStatusCode.BadRequest, currency.StatusCode);
        var responses = await Task.WhenAll(client.PostAsJsonAsync(path, request), second.PostAsJsonAsync(path, request));
        foreach (var response in responses) { var data = await DataAsync<OrderPaymentSummaryDto>(response); Assert.Equal(PaymentStatus.Paid, data.PaymentStatus); Assert.Single(data.Receipts); response.Dispose(); }
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.Set<OrderPaymentReceipt>().CountAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.OrderShipments.CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        Assert.Equal(0, carrier.Calls);
        var receipt = await fixture.ExecuteDbAsync(db => db.Set<OrderPaymentReceipt>().SingleAsync());
        Assert.StartsWith("admin-session:", receipt.Actor); Assert.Equal("Verified bank statement", receipt.Reason);
    }

    [Fact]
    public async Task TransferConfirmationAndCancelRace_DoNotDispatchOrResurrectCancelledOrder()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier());
        using var first = factory.CreateClient(); using var second = factory.CreateClient();
        var quote = await QuoteAsync(first, Input() with { PaymentMethod = PaymentMethod.ManualBankTransfer });
        using var placed = await first.PostAsJsonAsync("/api/checkout", Accept(quote, payment: PaymentMethod.ManualBankTransfer));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        var token = await first.LoginAsAdminAsync(); first.UseBearerToken(token); second.UseBearerToken(token);
        var results = await Task.WhenAll(first.PostAsJsonAsync($"/api/admin/orders/{order.Id}/payment/confirm-transfer", new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "RACE-BANK", "Statement")), second.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)OrderStatus.Cancelled, cancellationReason = "Cancellation race" }));
        Assert.True(results[1].IsSuccessStatusCode);
        var persisted = await fixture.ExecuteDbAsync(db => db.Orders.SingleAsync());
        Assert.Equal(OrderStatus.Cancelled, persisted.Status);
        Assert.Contains(persisted.PaymentStatus, new[] { PaymentStatus.Cancelled, PaymentStatus.RefundPending });
        Assert.Equal("Cancelled", await fixture.ExecuteDbAsync(db => db.OrderShipments.Select(x => x.ProviderStatus).SingleAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        foreach (var result in results) result.Dispose();
    }

    [Fact]
    public async Task CodCollectionAndRemittance_AreDistinctExactFacts_AndDeliveryAloneDoesNotPay()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier());
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client);
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        client.UseBearerToken(await client.LoginAsAdminAsync());
        var root = $"/api/admin/orders/{order.Id}/payment";
        var receipt = new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "SYNTHETIC-CASH", "Counted cash");
        using var premature = await client.PostAsJsonAsync($"{root}/collect-cod", receipt); Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        using var remitFirst = await client.PostAsJsonAsync($"{root}/remit-cod", receipt); Assert.Equal(HttpStatusCode.Conflict, remitFirst.StatusCode);
        // SHIP-10 owns local dispatch. Seed its future legal order stages to test
        // the payment boundary without exposing a premature lifecycle endpoint.
        await fixture.SeedAsync(async db => { var row = await db.Orders.SingleAsync(); foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Shipping }) db.Add(row.ChangeStatus(Guid.NewGuid(), status, "Synthetic lifecycle", "test")); });
        using var partial = await client.PostAsJsonAsync($"{root}/collect-cod", receipt with { Amount = 1 }); Assert.Equal(HttpStatusCode.Conflict, partial.StatusCode);
        using var collected = await client.PostAsJsonAsync($"{root}/collect-cod", receipt);
        var collection = await DataAsync<OrderPaymentSummaryDto>(collected);
        Assert.Equal(PaymentStatus.Paid, collection.PaymentStatus);
        Assert.Single(collection.Receipts); Assert.True(collection.CanRemitCod);
        using var duplicate = await client.PostAsJsonAsync($"{root}/collect-cod", receipt); Assert.True(duplicate.IsSuccessStatusCode);
        await fixture.SeedAsync(async db => { var row = await db.Orders.SingleAsync(); row.ChangeStatus(Guid.NewGuid(), OrderStatus.FailedDelivery, "Test failed delivery", "test"); row.ChangeStatus(Guid.NewGuid(), OrderStatus.Cancelled, "Test closed order", "test"); });
        using var summary = await client.GetAsync(root); Assert.True((await DataAsync<OrderPaymentSummaryDto>(summary)).CanRemitCod);
        using var remitted = await client.PostAsJsonAsync($"{root}/remit-cod", receipt with { Reference = "SYNTHETIC-REMIT" });
        var remittance = await DataAsync<OrderPaymentSummaryDto>(remitted);
        Assert.Equal(2, remittance.Receipts.Count); Assert.False(remittance.CanRemitCod);
        Assert.Equal(order.TotalAmount, remittance.TotalAmount);
        Assert.Equal(OrderStatus.Cancelled, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.Status).SingleAsync()));
    }

    [Fact]
    public async Task ExternalDeliveredCod_WaitsForFullCollectionBeforeCompletion()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier { Available = true });
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { Method = ShippingMethod.MiniLogistics });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderShipmentService>().ProcessDueCommandsAsync(10);
        var shipment = await fixture.ExecuteDbAsync(db => db.OrderShipments.SingleAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IShipmentWebhookService>().HandleAsync(new ShipmentWebhookPayload(Guid.NewGuid(), ShipmentProviderContract.ShipmentStatusChangedEvent, shipment.TrackingCode, order.OrderCode, "Delivered", DateTimeOffset.UtcNow));
            Assert.True(result.IsSuccess);
        }
        Assert.Equal(OrderStatus.Shipping, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.Status).SingleAsync()));
        Assert.Equal(PaymentStatus.Unpaid, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.PaymentStatus).SingleAsync()));
        Assert.Null(await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.CompletedAt).SingleAsync()));
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var bypass = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)OrderStatus.Completed });
        Assert.Equal(HttpStatusCode.Conflict, bypass.StatusCode);
        var path = $"/api/admin/orders/{order.Id}/payment/collect-cod";
        var receipt = new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "DELIVERED-CASH", "Cash verified after delivery");
        using var collected = await client.PostAsJsonAsync(path, receipt); Assert.True(collected.IsSuccessStatusCode);
        using var duplicate = await client.PostAsJsonAsync(path, receipt); Assert.True(duplicate.IsSuccessStatusCode);
        Assert.Equal(OrderStatus.Completed, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.Status).SingleAsync()));
        Assert.Equal(PaymentStatus.Paid, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.PaymentStatus).SingleAsync()));
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.OrderStatusHistories.CountAsync(x => x.ToStatus == OrderStatus.Completed)));
    }

    [Fact]
    public async Task ExternalTransfer_DoesNotCreateWorkOrAllowDispatchBeforeConfirmation()
    {
        await SeedAsync();
        await using var factory = Factory(new CountingCarrier { Available = true });
        using var client = factory.CreateClient();
        var quote = await QuoteAsync(client, Input() with { Method = ShippingMethod.MiniLogistics, PaymentMethod = PaymentMethod.ManualBankTransfer });
        using var placed = await client.PostAsJsonAsync("/api/checkout", Accept(quote, payment: PaymentMethod.ManualBankTransfer));
        var order = (await DataAsync<CheckoutResponse>(placed)).Order;
        Assert.Equal(0, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        client.UseBearerToken(await client.LoginAsAdminAsync());
        foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing })
        {
            using var advanced = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)status });
            Assert.True(advanced.IsSuccessStatusCode);
        }
        using var denied = await client.PutAsJsonAsync($"/api/admin/orders/{order.Id}/status", new { status = (int)OrderStatus.Shipping });
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        var path = $"/api/admin/orders/{order.Id}/payment/confirm-transfer";
        var receipt = new ConfirmOrderPaymentRequest(order.TotalAmount, "VND", "EXTERNAL-TRANSFER", "Verified statement");
        using var paid = await client.PostAsJsonAsync(path, receipt); Assert.True(paid.IsSuccessStatusCode);
        using var repeat = await client.PostAsJsonAsync(path, receipt); Assert.True(repeat.IsSuccessStatusCode);
        Assert.Equal(1, await fixture.ExecuteDbAsync(db => db.ShipmentCommandOutbox.CountAsync()));
        Assert.Equal(PaymentStatus.Paid, await fixture.ExecuteDbAsync(db => db.Orders.Select(x => x.PaymentStatus).SingleAsync()));
    }
}
