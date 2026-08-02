using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Shipments;

internal sealed class ShipmentCommandOutboxConfiguration : IEntityTypeConfiguration<ShipmentCommandOutbox>
{
    public void Configure(EntityTypeBuilder<ShipmentCommandOutbox> builder)
    {
        builder.ToTable("shipment_command_outbox", "shipping");
        builder.HasKey(command => command.Id);

        builder.Property(command => command.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(command => command.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(command => command.CommandType).HasColumnName("command_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(command => command.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(command => command.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(command => command.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc").IsRequired();
        builder.Property(command => command.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(command => command.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(command => command.LastError).HasColumnName("last_error").HasMaxLength(2000);

        builder.HasIndex(command => new { command.OrderId, command.CommandType })
            .IsUnique()
            .HasFilter("completed_at_utc IS NULL")
            .HasDatabaseName("ux_shipment_command_outbox_active_order_type");
        builder.HasIndex(command => new { command.CompletedAtUtc, command.NextAttemptAtUtc })
            .HasDatabaseName("ix_shipment_command_outbox_due");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(command => command.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
