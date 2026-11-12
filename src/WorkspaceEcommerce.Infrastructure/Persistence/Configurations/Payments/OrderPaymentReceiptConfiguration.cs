using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Payments;

internal sealed class OrderPaymentReceiptConfiguration : IEntityTypeConfiguration<OrderPaymentReceipt>
{
    public void Configure(EntityTypeBuilder<OrderPaymentReceipt> builder)
    {
        builder.ToTable("order_payment_receipts", "payments", table =>
        {
            table.HasCheckConstraint("ck_order_payment_receipt_amount", "amount >= 0 AND currency = 'VND'");
            table.HasCheckConstraint("ck_order_payment_receipt_kind", "kind IN ('ManualTransfer', 'CodCollected', 'CodRemitted', 'FullRefund')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 0);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(256).IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.HasIndex(x => new { x.OrderId, x.Kind }).IsUnique();
        builder.HasIndex(x => new { x.Kind, x.Reference }).IsUnique();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
