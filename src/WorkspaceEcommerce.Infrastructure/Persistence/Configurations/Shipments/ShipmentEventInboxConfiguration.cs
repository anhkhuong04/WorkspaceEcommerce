using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Shipments;

internal sealed class ShipmentEventInboxConfiguration : IEntityTypeConfiguration<ShipmentEventInbox>
{
    public void Configure(EntityTypeBuilder<ShipmentEventInbox> builder)
    {
        builder.ToTable("shipment_event_inbox", "shipping");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(entry => entry.EventName).HasColumnName("event_name").HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.TrackingCode).HasColumnName("tracking_code").HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.ExternalOrderId).HasColumnName("external_order_id").HasMaxLength(50).IsRequired();
        builder.Property(entry => entry.ProviderStatus).HasColumnName("provider_status").HasMaxLength(50).IsRequired();
        builder.Property(entry => entry.ChangedAtUtc).HasColumnName("changed_at_utc").IsRequired();
        builder.Property(entry => entry.ReceivedAtUtc).HasColumnName("received_at_utc").IsRequired();
        builder.Property(entry => entry.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(entry => entry.ProcessingError).HasColumnName("processing_error").HasMaxLength(2000);

        builder.HasIndex(entry => entry.ReceivedAtUtc).HasDatabaseName("ix_shipment_event_inbox_received_at_utc");
    }
}
