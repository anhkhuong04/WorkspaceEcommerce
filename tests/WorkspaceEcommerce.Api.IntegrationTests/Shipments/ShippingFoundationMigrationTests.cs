using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

public sealed class ShippingFoundationMigrationTests
{
    [Fact]
    public async Task Upgrade_PreservesLegacyMoneyPaymentProviderIdentityAndPendingWork()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("shipping_upgrade").WithUsername("shipping_test").WithPassword("shipping_test").Build();
        await postgres.StartAsync();
        await using var source = new NpgsqlDataSourceBuilder(postgres.GetConnectionString()).Build();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(source).Options;
        var now = DateTimeOffset.UtcNow;
        var order = new Order(Guid.NewGuid(), "LEGACY-SHIPPING-UPGRADE", null, "Synthetic customer", "0900000000",
            null, "Synthetic address", null, PaymentMethod.VNPay, "VND", 1);
        order.SetShippingFee(12345);
        order.MarkPaymentPaid(now);
        var providerId = Guid.NewGuid();
        order.UpdateShipmentInfo("LEGACY-TRACKING", providerId);
        await using (var context = new AppDbContext(options))
        {
            await context.GetService<IMigrator>().MigrateAsync("20260924001622_EnforceWarrantyCoverageSnapshotIdentity");
            // Seed the historical schema explicitly; today's EF model contains additive columns.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO ordering.orders
                (id, order_code, customer_name, customer_phone, shipping_address, payment_method, payment_status,
                 status, currency_code, exchange_rate, subtotal, shipping_fee, discount_amount, total_amount,
                 paid_at, created_at, updated_at, tracking_code, shipment_id)
                VALUES ({order.Id}, {order.OrderCode}, {order.CustomerName}, {order.CustomerPhone}, {order.ShippingAddress},
                        'VNPay', 'Paid', 'Pending', 'VND', 1, 0, 12345, 0, 12345, {now}, {now}, {now}, 'LEGACY-TRACKING', {providerId})
                """);
            var payment = new PaymentTransaction(Guid.NewGuid(), order.Id, PaymentProvider.VNPay, 12345, "VND", "LEGACY-TXN");
            payment.MarkSuccess("synthetic", "00", "success", null, null, now);
            context.Add(payment);
            context.Add(new ShipmentCommandOutbox(Guid.NewGuid(), order.Id, ShipmentCommandType.Cancel, "synthetic", now));
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO shipping.order_shipments
                (id, order_id, provider, provider_shipment_id, tracking_code, provider_status,
                 shipping_fee_amount, currency, last_synced_at_utc, last_event_at_utc, created_at_utc, updated_at_utc)
                VALUES ({Guid.NewGuid()}, {order.Id}, 'MiniLogistics', {providerId}, 'LEGACY-TRACKING',
                        'PendingPickup', 12345, 'VND', {now}, {now}, {now}, {now})
                """);
            await context.Database.MigrateAsync();
        }
        await using (var context = new AppDbContext(options))
        {
            var persisted = await context.Orders.SingleAsync();
            Assert.Equal(12345, persisted.TotalAmount);
            Assert.Equal(12345, persisted.ShippingFee);
            Assert.Equal(PaymentStatus.Paid, persisted.PaymentStatus);
            Assert.Equal(providerId, persisted.ShipmentId);
            var shipment = await context.OrderShipments.SingleAsync();
            Assert.Equal(providerId, shipment.ProviderShipmentId);
            Assert.Equal(12345, shipment.ShippingFeeAmount);
            Assert.Null(shipment.ActualCostAmount);
            Assert.Equal(PaymentTransactionStatus.Success, (await context.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(ShipmentCommandStatus.Pending, (await context.ShipmentCommandOutbox.SingleAsync()).Status);
            Assert.Empty(await context.Set<OrderShippingSelection>().ToArrayAsync());
            Assert.Empty(await context.Set<ShippingRatePlan>().ToArrayAsync());

            // A fresh tracker proves the database constraint; tracked one-to-one
            // fixup can otherwise schedule deletion of the existing dependent.
            context.ChangeTracker.Clear();
            var local = OrderShipment.CreateWorkspace(Guid.NewGuid(), order.Id, "LOCAL-CONSTRAINT", now);
            context.Add(local);
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        }
    }
}
