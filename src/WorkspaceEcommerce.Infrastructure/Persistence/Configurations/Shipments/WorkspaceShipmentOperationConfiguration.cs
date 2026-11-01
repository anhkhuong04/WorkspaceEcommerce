using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Shipments;

internal sealed class WorkspaceShipmentOperationConfiguration : IEntityTypeConfiguration<WorkspaceShipmentOperation>
{
    public void Configure(EntityTypeBuilder<WorkspaceShipmentOperation> builder)
    {
        builder.ToTable("workspace_operations", "shipping", table => table.HasCheckConstraint("ck_workspace_operation_revision", "revision > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ShipmentId).HasColumnName("shipment_id");
        builder.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Revision).HasColumnName("revision");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(x => x.AssignedTo).HasColumnName("assigned_to").HasMaxLength(120);
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.HasIndex(x => new { x.ShipmentId, x.Revision }).IsUnique();
        builder.HasOne<OrderShipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
