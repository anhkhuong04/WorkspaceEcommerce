using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Coupons;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Coupons;

internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons", "promotions");

        builder.HasKey(coupon => coupon.Id);

        builder.Property(coupon => coupon.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(coupon => coupon.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(coupon => coupon.Name)
            .HasColumnName("name")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(coupon => coupon.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(coupon => coupon.DiscountType)
            .HasColumnName("discount_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(coupon => coupon.DiscountValue)
            .HasColumnName("discount_value")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(coupon => coupon.MaxDiscountAmount)
            .HasColumnName("max_discount_amount")
            .HasPrecision(18, 2);

        builder.Property(coupon => coupon.MinimumSubtotal)
            .HasColumnName("minimum_subtotal")
            .HasPrecision(18, 2);

        builder.Property(coupon => coupon.StartsAt)
            .HasColumnName("starts_at");

        builder.Property(coupon => coupon.EndsAt)
            .HasColumnName("ends_at");

        builder.Property(coupon => coupon.UsageLimit)
            .HasColumnName("usage_limit");

        builder.Property(coupon => coupon.UsedCount)
            .HasColumnName("used_count")
            .IsRequired();

        builder.Property(coupon => coupon.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(coupon => coupon.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(coupon => coupon.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(coupon => coupon.Code)
            .IsUnique()
            .HasDatabaseName("ux_coupons_code");

        builder.HasIndex(coupon => coupon.IsActive)
            .HasDatabaseName("ix_coupons_is_active");

        builder.HasIndex(coupon => coupon.StartsAt)
            .HasDatabaseName("ix_coupons_starts_at");

        builder.HasIndex(coupon => coupon.EndsAt)
            .HasDatabaseName("ix_coupons_ends_at");

        builder.HasMany(coupon => coupon.ProductTargets)
            .WithOne()
            .HasForeignKey(target => target.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(coupon => coupon.ProductTargets)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
