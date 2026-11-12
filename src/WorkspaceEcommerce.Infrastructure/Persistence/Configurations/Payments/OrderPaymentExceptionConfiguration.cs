using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Payments;

internal sealed class OrderPaymentExceptionConfiguration : IEntityTypeConfiguration<OrderPaymentException>
{
    public void Configure(EntityTypeBuilder<OrderPaymentException> b)
    {
        b.ToTable("order_payment_exceptions", "payments"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ExpectedAmount).HasPrecision(18, 2); b.Property(x => x.ActualAmount).HasPrecision(18, 2); b.Property(x => x.SettlementAmount).HasPrecision(18, 2);
        b.Property(x => x.Reference).HasMaxLength(120); b.Property(x => x.SettlementReference).HasMaxLength(120);
        b.Property(x => x.Actor).HasMaxLength(256); b.Property(x => x.SettledBy).HasMaxLength(256);
        b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.SettlementReason).HasMaxLength(500); b.Property(x => x.Settlement).HasMaxLength(40);
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrderId, x.RequestId }).IsUnique(); b.HasIndex(x => new { x.OrderId, x.Reference }).IsUnique(); b.HasIndex(x => x.SettledAt);
        OrderReturnConfiguration.MapNames(b);
    }
}
