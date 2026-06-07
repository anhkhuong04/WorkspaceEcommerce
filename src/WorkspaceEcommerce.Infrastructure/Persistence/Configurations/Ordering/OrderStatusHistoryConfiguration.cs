using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Ordering;

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history", "ordering");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(history => history.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(history => history.FromStatus)
            .HasColumnName("from_status")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(history => history.ToStatus)
            .HasColumnName("to_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(history => history.Note)
            .HasColumnName("note")
            .HasMaxLength(1000);

        builder.Property(history => history.ChangedBy)
            .HasColumnName("changed_by")
            .HasMaxLength(250);

        builder.Property(history => history.ChangedAt)
            .HasColumnName("changed_at")
            .IsRequired();

        builder.HasIndex(history => history.OrderId)
            .HasDatabaseName("ix_order_status_history_order_id");

        builder.HasIndex(history => history.ChangedAt)
            .HasDatabaseName("ix_order_status_history_changed_at");
    }
}
