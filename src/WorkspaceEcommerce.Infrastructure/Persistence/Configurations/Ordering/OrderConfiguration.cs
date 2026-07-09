using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Ordering;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "ordering");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(order => order.OrderCode)
            .HasColumnName("order_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(order => order.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(order => order.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(order => order.CustomerPhone)
            .HasColumnName("customer_phone")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(order => order.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(250);

        builder.Property(order => order.ShippingAddress)
            .HasColumnName("shipping_address")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(order => order.Note)
            .HasColumnName("note")
            .HasMaxLength(1000);

        builder.Property(order => order.CouponId)
            .HasColumnName("coupon_id");

        builder.Property(order => order.CouponCodeSnapshot)
            .HasColumnName("coupon_code_snapshot")
            .HasMaxLength(50);

        builder.Property(order => order.CouponNameSnapshot)
            .HasColumnName("coupon_name_snapshot")
            .HasMaxLength(250);

        builder.Property(order => order.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(order => order.ShippingFee)
            .HasColumnName("shipping_fee")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(order => order.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(order => order.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(order => order.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(order => order.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(order => order.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(order => order.TrackingCode)
            .HasColumnName("tracking_code")
            .HasMaxLength(100);

        builder.Property(order => order.ShipmentId)
            .HasColumnName("shipment_id");

        builder.HasIndex(order => order.OrderCode)
            .IsUnique()
            .HasDatabaseName("ux_orders_order_code");

        builder.HasIndex(order => order.CustomerPhone)
            .HasDatabaseName("ix_orders_customer_phone");

        builder.HasIndex(order => order.CustomerId)
            .HasDatabaseName("ix_orders_customer_id");

        builder.HasIndex(order => order.CouponId)
            .HasDatabaseName("ix_orders_coupon_id");

        builder.HasIndex(order => order.Status)
            .HasDatabaseName("ix_orders_status");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(order => order.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(order => order.StatusHistory)
            .WithOne()
            .HasForeignKey(history => history.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(order => order.StatusHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
