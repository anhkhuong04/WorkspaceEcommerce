using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Ordering;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", "ordering");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(item => item.ProductVariantId)
            .HasColumnName("product_variant_id")
            .IsRequired();

        builder.Property(item => item.ProductNameSnapshot)
            .HasColumnName("product_name_snapshot")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(item => item.SkuSnapshot)
            .HasColumnName("sku_snapshot")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(item => item.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(item => item.RequiresInstallation)
            .HasColumnName("requires_installation")
            .IsRequired();

        builder.HasIndex(item => item.OrderId)
            .HasDatabaseName("ix_order_items_order_id");

        builder.HasIndex(item => item.ProductVariantId)
            .HasDatabaseName("ix_order_items_product_variant_id");

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(item => item.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
