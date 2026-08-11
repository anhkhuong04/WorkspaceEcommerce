using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class WarrantyAuditEventConfiguration : IEntityTypeConfiguration<WarrantyAuditEvent>
{
    public void Configure(EntityTypeBuilder<WarrantyAuditEvent> builder)
    {
        builder.ToTable("warranty_audit_events", "warranty");
        builder.HasKey(@event => @event.Id);
        builder.Property(@event => @event.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(@event => @event.WarrantyEntitlementId).HasColumnName("warranty_entitlement_id");
        builder.Property(@event => @event.SerializedProductUnitId).HasColumnName("serialized_product_unit_id");
        builder.Property(@event => @event.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(@event => @event.ActorType).HasColumnName("actor_type").HasMaxLength(30).IsRequired();
        builder.Property(@event => @event.ActorId).HasColumnName("actor_id").HasMaxLength(250).IsRequired();
        builder.Property(@event => @event.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(@event => @event.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(@event => @event.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.HasIndex(@event => new { @event.WarrantyEntitlementId, @event.OccurredAt, @event.Id })
            .IsDescending(false, true, false)
            .HasDatabaseName("ix_warranty_audit_entitlement_occurred_id");
        builder.HasIndex(@event => new { @event.SerializedProductUnitId, @event.OccurredAt, @event.Id })
            .IsDescending(false, true, false)
            .HasDatabaseName("ix_warranty_audit_unit_occurred_id");
        builder.HasOne<WarrantyEntitlement>().WithMany().HasForeignKey(@event => @event.WarrantyEntitlementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SerializedProductUnit>().WithMany().HasForeignKey(@event => @event.SerializedProductUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}
