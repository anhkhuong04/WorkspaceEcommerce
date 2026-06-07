using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class ProductSpecificationConfiguration : IEntityTypeConfiguration<ProductSpecification>
{
    public void Configure(EntityTypeBuilder<ProductSpecification> builder)
    {
        builder.ToTable("product_specifications", "catalog");

        builder.HasKey(specification => specification.Id);

        builder.Property(specification => specification.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(specification => specification.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(specification => specification.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(specification => specification.Value)
            .HasColumnName("value")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(specification => specification.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(specification => specification.ProductId)
            .HasDatabaseName("ix_product_specifications_product_id");

        builder.HasIndex(specification => new { specification.ProductId, specification.SortOrder })
            .HasDatabaseName("ix_product_specifications_product_id_sort_order");
    }
}
