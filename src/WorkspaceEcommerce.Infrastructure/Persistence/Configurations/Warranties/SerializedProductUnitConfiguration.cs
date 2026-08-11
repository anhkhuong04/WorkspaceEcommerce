using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class SerializedProductUnitConfiguration : IEntityTypeConfiguration<SerializedProductUnit>
{
    public void Configure(EntityTypeBuilder<SerializedProductUnit> builder)
    {
        builder.ToTable("serialized_product_units", "warranty");
        builder.HasKey(unit => unit.Id);
        builder.Property(unit => unit.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(unit => unit.ProductVariantId).HasColumnName("product_variant_id").IsRequired();
        builder.Property(unit => unit.IdentifierType).HasColumnName("identifier_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(unit => unit.IdentifierKeyVersion).HasColumnName("identifier_key_version").IsRequired();
        builder.Property(unit => unit.IdentifierFingerprint).HasColumnName("identifier_fingerprint").HasMaxLength(128).IsRequired();
        builder.Property(unit => unit.MaskedIdentifier).HasColumnName("masked_identifier").HasMaxLength(80).IsRequired();
        builder.Property(unit => unit.ImportBatchId).HasColumnName("import_batch_id").IsRequired();
        builder.Property(unit => unit.OrderItemId).HasColumnName("order_item_id");
        builder.Property(unit => unit.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(unit => unit.AssignedAt).HasColumnName("assigned_at");
        builder.Property(unit => unit.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(unit => unit.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(unit => new { unit.IdentifierType, unit.IdentifierKeyVersion, unit.IdentifierFingerprint })
            .IsUnique()
            .HasDatabaseName("ux_warranty_units_identifier_fingerprint");
        builder.HasIndex(unit => new { unit.ProductVariantId, unit.Status }).HasDatabaseName("ix_warranty_units_variant_status");
        builder.HasIndex(unit => unit.OrderItemId).HasDatabaseName("ix_warranty_units_order_item_id");
        builder.HasIndex(unit => unit.ImportBatchId).HasDatabaseName("ix_warranty_units_import_batch_id");
        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(unit => unit.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderItem>().WithMany().HasForeignKey(unit => unit.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarrantyImportBatch>().WithMany().HasForeignKey(unit => unit.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
