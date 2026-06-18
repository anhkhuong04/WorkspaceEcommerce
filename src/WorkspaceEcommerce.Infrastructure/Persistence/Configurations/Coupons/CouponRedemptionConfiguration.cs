using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Coupons;

internal sealed class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("coupon_redemptions", "promotions");

        builder.HasKey(redemption => redemption.Id);

        builder.Property(redemption => redemption.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(redemption => redemption.CouponId)
            .HasColumnName("coupon_id")
            .IsRequired();

        builder.Property(redemption => redemption.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(redemption => redemption.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(redemption => redemption.CustomerPhone)
            .HasColumnName("customer_phone")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(redemption => redemption.CodeSnapshot)
            .HasColumnName("code_snapshot")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(redemption => redemption.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(redemption => redemption.RedeemedAt)
            .HasColumnName("redeemed_at")
            .IsRequired();

        builder.HasIndex(redemption => redemption.CouponId)
            .HasDatabaseName("ix_coupon_redemptions_coupon_id");

        builder.HasIndex(redemption => redemption.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_coupon_redemptions_order_id");

        builder.HasIndex(redemption => redemption.CustomerId)
            .HasDatabaseName("ix_coupon_redemptions_customer_id");

        builder.HasIndex(redemption => redemption.CodeSnapshot)
            .HasDatabaseName("ix_coupon_redemptions_code_snapshot");

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(redemption => redemption.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(redemption => redemption.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(redemption => redemption.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
