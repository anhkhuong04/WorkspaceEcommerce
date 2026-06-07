using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Catalog;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", "catalog");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(category => category.ParentId)
            .HasColumnName("parent_id");

        builder.Property(category => category.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(category => category.Slug)
            .HasColumnName("slug")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(category => category.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(category => category.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.HasIndex(category => category.Slug)
            .IsUnique()
            .HasDatabaseName("ux_categories_slug");

        builder.HasIndex(category => category.ParentId)
            .HasDatabaseName("ix_categories_parent_id");

        builder.HasIndex(category => category.IsActive)
            .HasDatabaseName("ix_categories_is_active");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(category => category.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
