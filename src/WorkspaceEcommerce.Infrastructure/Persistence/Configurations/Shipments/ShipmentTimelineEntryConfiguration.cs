using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Shipments;

internal sealed class ShipmentTimelineEntryConfiguration : IEntityTypeConfiguration<ShipmentTimelineEntry>
{
    public void Configure(EntityTypeBuilder<ShipmentTimelineEntry> builder)
    {
        builder.ToTable("shipment_timeline_entries", "shipping");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.OrderShipmentId).HasColumnName("order_shipment_id").IsRequired();
        builder.Property(entry => entry.ProviderStatus).HasColumnName("provider_status").HasMaxLength(50).IsRequired();
        builder.Property(entry => entry.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(entry => entry.ChangedAtUtc).HasColumnName("changed_at_utc").IsRequired();
        builder.Property(entry => entry.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(entry => entry.ProviderEventId).HasColumnName("provider_event_id");
        builder.Property(entry => entry.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(entry => new { entry.OrderShipmentId, entry.ChangedAtUtc, entry.ProviderStatus })
            .IsUnique()
            .HasDatabaseName("ux_shipment_timeline_state_time");
        builder.HasIndex(entry => entry.ProviderEventId)
            .IsUnique()
            .HasFilter("provider_event_id IS NOT NULL")
            .HasDatabaseName("ux_shipment_timeline_provider_event_id");

        builder.HasOne<OrderShipment>()
            .WithMany()
            .HasForeignKey(entry => entry.OrderShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
