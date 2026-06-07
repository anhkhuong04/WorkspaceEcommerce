using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Cart;

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items", "cart");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.CartId)
            .HasColumnName("cart_id")
            .IsRequired();

        builder.Property(item => item.ProductVariantId)
            .HasColumnName("product_variant_id")
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(item => item.UnitPriceSnapshot)
            .HasColumnName("unit_price_snapshot")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(item => item.CartId)
            .HasDatabaseName("ix_cart_items_cart_id");

        builder.HasIndex(item => item.ProductVariantId)
            .HasDatabaseName("ix_cart_items_product_variant_id");

        builder.HasIndex(item => new { item.CartId, item.ProductVariantId })
            .IsUnique()
            .HasDatabaseName("ux_cart_items_cart_id_product_variant_id");

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(item => item.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
