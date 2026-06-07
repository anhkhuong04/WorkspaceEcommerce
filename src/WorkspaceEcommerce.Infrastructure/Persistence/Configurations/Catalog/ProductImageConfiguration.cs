using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images", "catalog");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(image => image.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(image => image.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(image => image.AltText)
            .HasColumnName("alt_text")
            .HasMaxLength(250);

        builder.Property(image => image.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(image => image.ProductId)
            .HasDatabaseName("ix_product_images_product_id");

        builder.HasIndex(image => new { image.ProductId, image.SortOrder })
            .HasDatabaseName("ix_product_images_product_id_sort_order");
    }
}
