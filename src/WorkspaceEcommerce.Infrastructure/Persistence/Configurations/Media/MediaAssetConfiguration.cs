using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Media;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Media;

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets", "content");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Folder).HasColumnName("folder").HasMaxLength(64).IsRequired();
        builder.Property(asset => asset.ObjectKey).HasColumnName("object_key").HasMaxLength(512).IsRequired();
        builder.HasIndex(asset => asset.ObjectKey).IsUnique();
        builder.Property(asset => asset.PublicUrl).HasColumnName("public_url").HasMaxLength(1024).IsRequired();
        builder.HasIndex(asset => asset.PublicUrl).IsUnique();
        builder.Property(asset => asset.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.Checksum).HasColumnName("checksum").HasMaxLength(128).IsRequired();
        builder.Property(asset => asset.Size).HasColumnName("size").IsRequired();
        builder.Property(asset => asset.Width).HasColumnName("width").IsRequired();
        builder.Property(asset => asset.Height).HasColumnName("height").IsRequired();
        builder.Property(asset => asset.FrameCount).HasColumnName("frame_count").IsRequired();
        builder.Property(asset => asset.CreatedBy).HasColumnName("created_by").HasMaxLength(256);
        builder.Property(asset => asset.State).HasColumnName("state").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(asset => asset.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(asset => asset.AvailableAt).HasColumnName("available_at");
        builder.Property(asset => asset.DeletedAt).HasColumnName("deleted_at");
        builder.Property(asset => asset.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);
        builder.HasIndex(asset => new { asset.State, asset.CreatedAt });
        builder.HasMany(asset => asset.Variants).WithOne().HasForeignKey(variant => variant.MediaAssetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MediaAssetVariantConfiguration : IEntityTypeConfiguration<MediaAssetVariant>
{
    public void Configure(EntityTypeBuilder<MediaAssetVariant> builder)
    {
        builder.ToTable("media_asset_variants", "content");
        builder.HasKey(variant => variant.Id);
        builder.Property(variant => variant.MediaAssetId).HasColumnName("media_asset_id").IsRequired();
        builder.Property(variant => variant.Name).HasColumnName("name").HasMaxLength(32).IsRequired();
        builder.Property(variant => variant.ObjectKey).HasColumnName("object_key").HasMaxLength(512).IsRequired();
        builder.Property(variant => variant.PublicUrl).HasColumnName("public_url").HasMaxLength(1024).IsRequired();
        builder.Property(variant => variant.Width).HasColumnName("width").IsRequired();
        builder.Property(variant => variant.Height).HasColumnName("height").IsRequired();
        builder.Property(variant => variant.Size).HasColumnName("size").IsRequired();
        builder.HasIndex(variant => new { variant.MediaAssetId, variant.Name }).IsUnique();
    }
}
