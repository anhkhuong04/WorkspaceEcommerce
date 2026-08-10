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
        builder.Property(command => command.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(command => command.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(200);
        builder.Property(command => command.LeaseToken)
            .HasColumnName("lease_token")
            .IsConcurrencyToken();
        builder.Property(command => command.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        builder.Property(command => command.LastAttemptAtUtc).HasColumnName("last_attempt_at_utc");
        builder.Property(command => command.DeadLetteredAtUtc).HasColumnName("dead_lettered_at_utc");
        builder.Property(command => command.LastErrorCategory).HasColumnName("last_error_category").HasMaxLength(100);

        builder.HasIndex(command => new { command.OrderId, command.CommandType })
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Leased')")
            .HasDatabaseName("ux_shipment_command_outbox_active_order_type");
        builder.HasIndex(command => new
            {
                command.Status,
                command.NextAttemptAtUtc,
                command.LeaseExpiresAtUtc,
                command.CreatedAtUtc,
                command.Id
            })
            .HasDatabaseName("ix_shipment_command_outbox_claim");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(command => command.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
