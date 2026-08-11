using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class WarrantyImportBatchConfiguration : IEntityTypeConfiguration<WarrantyImportBatch>
{
    public void Configure(EntityTypeBuilder<WarrantyImportBatch> builder)
    {
        builder.ToTable("warranty_import_batches", "warranty");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(batch => batch.ContentChecksum).HasColumnName("content_checksum").HasMaxLength(128).IsRequired();
        builder.Property(batch => batch.RequestedBy).HasColumnName("requested_by").HasMaxLength(250).IsRequired();
        builder.Property(batch => batch.TotalRows).HasColumnName("total_rows").IsRequired();
        builder.Property(batch => batch.ImportedRows).HasColumnName("imported_rows").IsRequired();
        builder.Property(batch => batch.FailedRows).HasColumnName("failed_rows").IsRequired();
        builder.Property(batch => batch.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(batch => batch.CompletedAt).HasColumnName("completed_at");
        builder.HasIndex(batch => batch.ContentChecksum).IsUnique().HasDatabaseName("ux_warranty_import_batches_checksum");
        builder.HasIndex(batch => new { batch.CreatedAt, batch.Id }).IsDescending(true, false).HasDatabaseName("ix_warranty_import_batches_created_id");
    }
}
