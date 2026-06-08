using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Content;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Content;

internal sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("banners", "content");

        builder.HasKey(banner => banner.Id);

        builder.Property(banner => banner.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(banner => banner.Title)
            .HasColumnName("title")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(banner => banner.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(banner => banner.LinkUrl)
            .HasColumnName("link_url")
            .HasMaxLength(1000);

        builder.Property(banner => banner.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(banner => banner.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(banner => banner.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(banner => banner.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(banner => banner.IsActive)
            .HasDatabaseName("ix_banners_is_active");

        builder.HasIndex(banner => banner.SortOrder)
            .HasDatabaseName("ix_banners_sort_order");
    }
}
