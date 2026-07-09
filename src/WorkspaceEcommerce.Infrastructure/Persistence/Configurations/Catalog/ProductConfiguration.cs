using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "catalog");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(product => product.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(product => product.Name)
            .HasColumnName("name")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(product => product.Slug)
            .HasColumnName("slug")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(product => product.IsFeatured)
            .HasColumnName("is_featured")
            .IsRequired();

        builder.Property(product => product.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(product => product.AverageRating)
            .HasColumnName("average_rating")
            .HasDefaultValue(0.0)
            .IsRequired();

        builder.Property(product => product.ReviewCount)
            .HasColumnName("review_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(product => product.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(product => product.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(product => product.Slug)
            .IsUnique()
            .HasDatabaseName("ux_products_slug");

        builder.HasIndex(product => product.CategoryId)
            .HasDatabaseName("ix_products_category_id");

        builder.HasIndex(product => product.IsActive)
            .HasDatabaseName("ix_products_is_active");

        builder.HasIndex(product => product.IsFeatured)
            .HasDatabaseName("ix_products_is_featured");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(product => product.Variants)
            .WithOne()
            .HasForeignKey(variant => variant.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(product => product.Images)
            .WithOne()
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(product => product.Specifications)
            .WithOne()
            .HasForeignKey(specification => specification.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(product => product.Variants)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(product => product.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(product => product.Specifications)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
