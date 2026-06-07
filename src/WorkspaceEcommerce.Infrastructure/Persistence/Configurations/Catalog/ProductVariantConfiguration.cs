using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants", "catalog");

        builder.HasKey(variant => variant.Id);

        builder.Property(variant => variant.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(variant => variant.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(variant => variant.Sku)
            .HasColumnName("sku")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(variant => variant.Name)
            .HasColumnName("name")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(variant => variant.Color)
            .HasColumnName("color")
            .HasMaxLength(100);

        builder.Property(variant => variant.Size)
            .HasColumnName("size")
            .HasMaxLength(100);

        builder.Property(variant => variant.Price)
            .HasColumnName("price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(variant => variant.CompareAtPrice)
            .HasColumnName("compare_at_price")
            .HasPrecision(18, 2);

        builder.Property(variant => variant.StockQuantity)
            .HasColumnName("stock_quantity")
            .IsRequired();

        builder.Property(variant => variant.RequiresInstallation)
            .HasColumnName("requires_installation")
            .IsRequired();

        builder.Property(variant => variant.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(variant => variant.Sku)
            .IsUnique()
            .HasDatabaseName("ux_product_variants_sku");

        builder.HasIndex(variant => variant.ProductId)
            .HasDatabaseName("ix_product_variants_product_id");

        builder.HasIndex(variant => variant.IsActive)
            .HasDatabaseName("ix_product_variants_is_active");
    }
}
