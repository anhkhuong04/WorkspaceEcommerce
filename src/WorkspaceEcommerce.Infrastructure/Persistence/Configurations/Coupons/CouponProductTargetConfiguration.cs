using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Coupons;

internal sealed class CouponProductTargetConfiguration : IEntityTypeConfiguration<CouponProductTarget>
{
    public void Configure(EntityTypeBuilder<CouponProductTarget> builder)
    {
        builder.ToTable("coupon_product_targets", "promotions");

        builder.HasKey(target => target.Id);

        builder.Property(target => target.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(target => target.CouponId)
            .HasColumnName("coupon_id")
            .IsRequired();

        builder.Property(target => target.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.HasIndex(target => target.CouponId)
            .HasDatabaseName("ix_coupon_product_targets_coupon_id");

        builder.HasIndex(target => target.ProductId)
            .HasDatabaseName("ix_coupon_product_targets_product_id");

        builder.HasIndex(target => new { target.CouponId, target.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_coupon_product_targets_coupon_id_product_id");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(target => target.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
