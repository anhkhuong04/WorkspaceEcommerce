using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class WarrantyCoverageSnapshotConfiguration : IEntityTypeConfiguration<WarrantyCoverageSnapshot>
{
    public void Configure(EntityTypeBuilder<WarrantyCoverageSnapshot> builder)
    {
        builder.ToTable("warranty_coverage_snapshots", "warranty");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(snapshot => snapshot.WarrantyEntitlementId).HasColumnName("warranty_entitlement_id").IsRequired();
        builder.Property(snapshot => snapshot.ComponentCode).HasColumnName("component_code").HasMaxLength(50).IsRequired();
        builder.Property(snapshot => snapshot.DisplayName).HasColumnName("display_name").HasMaxLength(250).IsRequired();
        builder.Property(snapshot => snapshot.DurationMonths).HasColumnName("duration_months").IsRequired();
        builder.Property(snapshot => snapshot.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(snapshot => snapshot.EndsAt).HasColumnName("ends_at").IsRequired();
        builder.Property(snapshot => snapshot.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.HasIndex(snapshot => new { snapshot.WarrantyEntitlementId, snapshot.SortOrder, snapshot.Id })
            .HasDatabaseName("ix_warranty_coverage_snapshots_entitlement_sort_id");
    }
}
