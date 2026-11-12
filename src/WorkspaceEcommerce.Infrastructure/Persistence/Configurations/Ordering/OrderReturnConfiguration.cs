using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Ordering;

internal sealed class OrderReturnConfiguration : IEntityTypeConfiguration<OrderReturnCase>
{
    public void Configure(EntityTypeBuilder<OrderReturnCase> b)
    {
        b.ToTable("order_returns", "ordering"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Ignore(x => x.RefundAmount);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.RequestFingerprint).HasMaxLength(64);
        b.Property(x => x.ReceiptFingerprint).HasMaxLength(64);
        b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(256); b.Property(x => x.RefundActor).HasMaxLength(256);
        b.Property(x => x.RefundReason).HasMaxLength(500); b.Property(x => x.RefundReference).HasMaxLength(120);
        b.Property(x => x.MerchandiseRefund).HasPrecision(18, 2); b.Property(x => x.ShippingRefund).HasPrecision(18, 2);
        b.HasIndex(x => new { x.OrderId, x.RequestId }).IsUnique();
        b.HasIndex(x => new { x.OrderId, x.RefundReference }).IsUnique().HasFilter("refund_reference IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        MapNames(b);
    }
    internal static void MapNames<T>(EntityTypeBuilder<T> b) where T : class
    {
        foreach (var property in b.Metadata.GetProperties())
            property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
internal sealed class OrderReturnLineConfiguration : IEntityTypeConfiguration<OrderReturnLine>
{
    public void Configure(EntityTypeBuilder<OrderReturnLine> b)
    {
        b.ToTable("order_return_lines", "ordering"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.RefundAmount).HasPrecision(18, 2); b.Property(x => x.Inspection).HasMaxLength(500);
        b.Property(x => x.SerializedUnitIds).HasColumnType("uuid[]");
        b.HasIndex(x => new { x.ReturnCaseId, x.OrderItemId }).IsUnique();
        b.HasOne<OrderReturnCase>().WithMany().HasForeignKey(x => x.ReturnCaseId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        OrderReturnConfiguration.MapNames(b);
    }
}
internal sealed class OrderReturnAuditConfiguration : IEntityTypeConfiguration<OrderReturnAudit>
{
    public void Configure(EntityTypeBuilder<OrderReturnAudit> b)
    {
        b.ToTable("order_return_audits", "ordering"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Actor).HasMaxLength(256); b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => new { x.ReturnCaseId, x.OccurredAt });
        b.HasOne<OrderReturnCase>().WithMany().HasForeignKey(x => x.ReturnCaseId).OnDelete(DeleteBehavior.Cascade);
        OrderReturnConfiguration.MapNames(b);
    }
}
