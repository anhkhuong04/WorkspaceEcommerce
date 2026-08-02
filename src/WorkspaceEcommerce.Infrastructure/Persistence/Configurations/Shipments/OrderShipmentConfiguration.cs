using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Shipments;

internal sealed class OrderShipmentConfiguration : IEntityTypeConfiguration<OrderShipment>
{
    public void Configure(EntityTypeBuilder<OrderShipment> builder)
    {
        builder.ToTable("order_shipments", "shipping");
        builder.HasKey(shipment => shipment.Id);

        builder.Property(shipment => shipment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(shipment => shipment.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(shipment => shipment.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(shipment => shipment.ProviderShipmentId).HasColumnName("provider_shipment_id").IsRequired();
        builder.Property(shipment => shipment.TrackingCode).HasColumnName("tracking_code").HasMaxLength(100).IsRequired();
        builder.Property(shipment => shipment.ProviderStatus).HasColumnName("provider_status").HasMaxLength(50).IsRequired();
        builder.Property(shipment => shipment.ShippingFeeAmount).HasColumnName("shipping_fee_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(shipment => shipment.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
        builder.Property(shipment => shipment.LastSyncedAtUtc).HasColumnName("last_synced_at_utc").IsRequired();
        builder.Property(shipment => shipment.LastEventAtUtc).HasColumnName("last_event_at_utc").IsRequired();
        builder.Property(shipment => shipment.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(shipment => shipment.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasIndex(shipment => shipment.OrderId).IsUnique().HasDatabaseName("ux_order_shipments_order_id");
        builder.HasIndex(shipment => shipment.TrackingCode).IsUnique().HasDatabaseName("ux_order_shipments_tracking_code");
        builder.HasIndex(shipment => new { shipment.Provider, shipment.ProviderShipmentId })
            .IsUnique()
            .HasDatabaseName("ux_order_shipments_provider_shipment_id");
        builder.HasIndex(shipment => shipment.ProviderStatus).HasDatabaseName("ix_order_shipments_provider_status");

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<OrderShipment>(shipment => shipment.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
